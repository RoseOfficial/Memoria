using Microsoft.EntityFrameworkCore;
using MemoriaServer.Data;
using System.Security.Claims;

namespace MemoriaServer.Middleware
{
    /// <summary>
    /// Custom authentication middleware for Memoria API key validation.
    /// Resolves the API key from the <c>api-key</c> header first, then falls back to the
    /// <c>__Host-memoria</c> cookie. Performs an exact opaque string match against
    /// <see cref="MemoriaServer.Models.Entities.ApplicationUser.ApiKey"/> so both
    /// plugin-issued keys and web-issued keys are supported without a fixed format.
    /// </summary>
    public class ApiKeyAuthenticationMiddleware
    {
        /// <summary>
        /// Next middleware in the ASP.NET Core pipeline
        /// </summary>
        private readonly RequestDelegate _next;
        
        /// <summary>
        /// Logger for authentication events and error tracking
        /// </summary>
        private readonly ILogger<ApiKeyAuthenticationMiddleware> _logger;

        /// <summary>
        /// Initializes the API key authentication middleware.
        /// </summary>
        /// <param name="next">Next middleware delegate in the pipeline</param>
        /// <param name="logger">Logger for authentication operations</param>
        public ApiKeyAuthenticationMiddleware(RequestDelegate next, ILogger<ApiKeyAuthenticationMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, MemoriaDbContext dbContext)
        {
            // Skip authentication for certain endpoints
            var path = context.Request.Path.Value?.ToLower();
            if (path != null && (
                path.Contains("/server") && context.Request.Method == "GET" ||
                path.Contains("/users/login") ||
                (path.Contains("/auth/") && !path.Contains("/auth/logout")) ||
                path.Contains("/swagger") ||
                path.Contains("/health") ||
                // Plan 0c public-read endpoints used by the anonymous web surface.
                // by-slug and recent return tier-filtered content; takedowns/POST is the
                // public submission form. Tier/admin gating happens downstream in each controller.
                (path.Contains("/players/recent") && context.Request.Method == "GET") ||
                (path.Contains("/players/by-slug") && context.Request.Method == "GET") ||
                (path.Contains("/players/search") && context.Request.Method == "GET") ||
                (path.EndsWith("/takedowns") && context.Request.Method == "POST")))
            {
                await _next(context);
                return;
            }

            // Resolve API key from header first, then fall back to the __Host-memoria cookie.
            var apiKey = context.Request.Headers.TryGetValue("api-key", out var headerValues)
                ? headerValues.FirstOrDefault()
                : null;

            if (string.IsNullOrEmpty(apiKey))
            {
                apiKey = context.Request.Cookies["__Host-memoria"];
            }

            if (string.IsNullOrEmpty(apiKey))
            {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("API key is required");
                return;
            }

            try
            {
                var user = await dbContext.Users.FirstOrDefaultAsync(u => u.ApiKey == apiKey);

                if (user == null)
                {
                    context.Response.StatusCode = 401;
                    await context.Response.WriteAsync("Invalid API key");
                    return;
                }

                var claims = new List<Claim>
                {
                    new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new(ClaimTypes.Name, user.Name),
                    new("AppRoleId", user.AppRoleId.ToString()),
                };
                if (user.GameAccountId.HasValue)
                    claims.Add(new Claim("GameAccountId", user.GameAccountId.Value.ToString()));
                if (user.DiscordUserId.HasValue)
                    claims.Add(new Claim("DiscordUserId", user.DiscordUserId.Value.ToString()));
                claims.Add(new Claim("LocalContentId", user.PrimaryCharacterLocalContentId.ToString()));

                var identity = new ClaimsIdentity(claims, "ApiKey");
                context.User = new ClaimsPrincipal(identity);

                context.Items["User"] = user;
                context.Items["UserId"] = user.Id;
                // Stored as int? since ApplicationUser.GameAccountId is nullable for web-first users.
                // Consumers should cast to `int?`, not `int`.
                context.Items["GameAccountId"] = user.GameAccountId;

                var timeSinceLastLogin = DateTime.UtcNow - user.LastLoginAt;
                if (timeSinceLastLogin.TotalMinutes > 5)
                {
                    user.LastLoginAt = DateTime.UtcNow;
                    await dbContext.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during API key authentication");
                context.Response.StatusCode = 500;
                await context.Response.WriteAsync("Authentication error");
                return;
            }

            await _next(context);
        }
    }

    /// <summary>
    /// Extension methods for registering the API key authentication middleware.
    /// </summary>
    public static class ApiKeyAuthenticationMiddlewareExtensions
    {
        /// <summary>
        /// Adds the API key authentication middleware to the application pipeline.
        /// Should be registered early in the pipeline, before authorization middleware.
        /// </summary>
        /// <param name="builder">Application builder for middleware registration</param>
        /// <returns>Application builder for method chaining</returns>
        public static IApplicationBuilder UseApiKeyAuthentication(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<ApiKeyAuthenticationMiddleware>();
        }
    }
}