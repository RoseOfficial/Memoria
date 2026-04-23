using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using AlphaScopeServer.Data;
using AlphaScopeServer.Models.Entities;
using AlphaScopeServer.Services.Auth;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Web;

namespace AlphaScopeServer.Controllers
{
    [ApiController]
    [Route("v1/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AlphaScopeDbContext _context;
        private readonly ILogger<AuthController> _logger;
        private readonly IOptions<DiscordOptions> _discordOptions;
        private readonly OAuthStateSigner _stateSigner;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        public AuthController(
            AlphaScopeDbContext context,
            ILogger<AuthController> logger,
            IOptions<DiscordOptions> discordOptions,
            OAuthStateSigner stateSigner,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration)
        {
            _context = context;
            _logger = logger;
            _discordOptions = discordOptions;
            _stateSigner = stateSigner;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        [HttpGet("discord/start")]
        public IActionResult StartDiscordOAuth([FromQuery] string return_to)
        {
            if (string.IsNullOrWhiteSpace(return_to) || !IsAllowedReturnTo(return_to))
                return BadRequest("Invalid return_to URL.");

            var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
            var state = _stateSigner.Sign(return_to, nonce);

            Response.Cookies.Append("__alpha_oauth_state", state, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                Path = "/",
                MaxAge = TimeSpan.FromMinutes(10),
            });

            var redirectUri = $"{_configuration["ServerBaseUrl"]}/v1/auth/discord/callback";
            var discordUrl = "https://discord.com/api/oauth2/authorize?" +
                $"client_id={HttpUtility.UrlEncode(_discordOptions.Value.ClientId)}" +
                $"&redirect_uri={HttpUtility.UrlEncode(redirectUri)}" +
                $"&response_type=code" +
                $"&scope={HttpUtility.UrlEncode("identify guilds")}" +
                $"&state={HttpUtility.UrlEncode(state)}";

            return Redirect(discordUrl);
        }

        [HttpGet("discord/callback")]
        public async Task<IActionResult> CallbackDiscordOAuth(
            [FromQuery] string code,
            [FromQuery] string state,
            CancellationToken ct)
        {
            // 1. Validate state against cookie
            if (!Request.Cookies.TryGetValue("__alpha_oauth_state", out var cookieState) || cookieState != state)
                return BadRequest("Invalid OAuth state cookie.");

            Response.Cookies.Delete("__alpha_oauth_state");

            if (!_stateSigner.Verify(state, out var returnTo, out _))
                return BadRequest("Invalid OAuth state signature.");

            var httpClient = _httpClientFactory.CreateClient("DiscordAuth");

            // 2. Exchange code for access token
            var tokenReq = new HttpRequestMessage(HttpMethod.Post, "oauth2/token")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = _discordOptions.Value.ClientId,
                    ["client_secret"] = _discordOptions.Value.ClientSecret,
                    ["grant_type"] = "authorization_code",
                    ["code"] = code,
                    ["redirect_uri"] = $"{_configuration["ServerBaseUrl"]}/v1/auth/discord/callback",
                }),
            };
            var tokenResp = await httpClient.SendAsync(tokenReq, ct);
            if (!tokenResp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Discord token exchange failed: {Status}", tokenResp.StatusCode);
                return StatusCode(StatusCodes.Status502BadGateway, "Discord token exchange failed.");
            }
            var tokenJson = await tokenResp.Content.ReadFromJsonAsync<DiscordTokenResponse>(cancellationToken: ct);
            if (tokenJson?.AccessToken is null)
                return StatusCode(StatusCodes.Status502BadGateway, "Invalid Discord token response.");

            // 3. Fetch /users/@me
            httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenJson.AccessToken);
            var meResp = await httpClient.GetAsync("users/@me", ct);
            if (!meResp.IsSuccessStatusCode)
                return StatusCode(StatusCodes.Status502BadGateway, "Discord /users/@me call failed.");
            var me = await meResp.Content.ReadFromJsonAsync<DiscordUser>(cancellationToken: ct);
            if (me?.Id is null || !long.TryParse(me.Id, out var discordUserId))
                return StatusCode(StatusCodes.Status502BadGateway, "Invalid Discord user payload.");

            // 4. Fetch /users/@me/guilds for membership check
            var guildsResp = await httpClient.GetAsync("users/@me/guilds", ct);
            var isGuildMember = false;
            if (guildsResp.IsSuccessStatusCode)
            {
                var guilds = await guildsResp.Content.ReadFromJsonAsync<List<DiscordGuild>>(cancellationToken: ct) ?? new();
                isGuildMember = guilds.Any(g => g.Id == _discordOptions.Value.GuildId);
            }

            // 5. Upsert ApplicationUser
            var existing = await _context.Users.FirstOrDefaultAsync(u => u.DiscordUserId == discordUserId, ct);
            ApplicationUser user;
            if (existing is null)
            {
                user = new ApplicationUser
                {
                    Name = me.Username ?? "DiscordUser",
                    DiscordUserId = discordUserId,
                    ApiKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)),
                    IsGuildMember = isGuildMember,
                    GuildMembershipCheckedAt = DateTime.UtcNow,
                    PrimaryCharacterLocalContentId = 0,
                    AppRoleId = (int)UserRole.Member,
                    CreatedAt = DateTime.UtcNow,
                    LastLoginAt = DateTime.UtcNow,
                };
                _context.Users.Add(user);
            }
            else
            {
                existing.IsGuildMember = isGuildMember;
                existing.GuildMembershipCheckedAt = DateTime.UtcNow;
                existing.LastLoginAt = DateTime.UtcNow;
                user = existing;
            }
            await _context.SaveChangesAsync(ct);

            // 6. Set __Host-alpha cookie and redirect back to web
            Response.Cookies.Append("__Host-alpha", user.ApiKey, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                Path = "/",
                MaxAge = TimeSpan.FromDays(30),
            });
            return Redirect(returnTo);
        }

        private bool IsAllowedReturnTo(string url)
        {
            var originsCsv = _configuration["Cors:AllowedOrigins"];
            if (string.IsNullOrWhiteSpace(originsCsv)) return false;

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;

            var allowed = originsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var origin in allowed)
            {
                if (Uri.TryCreate(origin, UriKind.Absolute, out var allowedUri) &&
                    uri.Scheme == allowedUri.Scheme &&
                    uri.Host == allowedUri.Host &&
                    uri.Port == allowedUri.Port)
                {
                    return true;
                }
            }
            return false;
        }

        private sealed record DiscordTokenResponse
        {
            [System.Text.Json.Serialization.JsonPropertyName("access_token")]
            public string? AccessToken { get; init; }
        }

        private sealed record DiscordUser
        {
            [System.Text.Json.Serialization.JsonPropertyName("id")]
            public string? Id { get; init; }
            [System.Text.Json.Serialization.JsonPropertyName("username")]
            public string? Username { get; init; }
        }

        private sealed record DiscordGuild
        {
            [System.Text.Json.Serialization.JsonPropertyName("id")]
            public string Id { get; init; } = string.Empty;
        }
    }
}
