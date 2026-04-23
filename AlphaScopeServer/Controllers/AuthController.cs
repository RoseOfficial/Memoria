using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using AlphaScopeServer.Data;
using AlphaScopeServer.Services.Auth;
using System.Security.Cryptography;
using System.Web;

namespace AlphaScopeServer.Controllers
{
    [ApiController]
    [Route("[controller]")]
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
    }
}
