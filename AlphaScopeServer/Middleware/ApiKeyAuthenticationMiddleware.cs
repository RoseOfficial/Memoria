using Microsoft.EntityFrameworkCore;
using AlphaScopeServer.Data;
using System.Security.Claims;

namespace AlphaScopeServer.Middleware
{
    public class ApiKeyAuthenticationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ApiKeyAuthenticationMiddleware> _logger;

        public ApiKeyAuthenticationMiddleware(RequestDelegate next, ILogger<ApiKeyAuthenticationMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, AlphaScopeDbContext dbContext)
        {
            // Skip authentication for certain endpoints
            var path = context.Request.Path.Value?.ToLower();
            if (path != null && (
                path.Contains("/server") && context.Request.Method == "GET" ||
                path.Contains("/users/login") ||
                path.Contains("/users/create-test-user") ||
                path.Contains("/auth/") ||
                path.Contains("/waitforlogin") ||
                path.Contains("/swagger") ||
                path.Contains("/health")))
            {
                await _next(context);
                return;
            }

            // Check for API key header
            if (!context.Request.Headers.TryGetValue("api-key", out var apiKeyValues))
            {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("API key is required");
                return;
            }

            var apiKey = apiKeyValues.FirstOrDefault();
            if (string.IsNullOrEmpty(apiKey))
            {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("API key is required");
                return;
            }

            // Parse API key format: {userKey}-{accountId}
            var keyParts = apiKey.Split('-');
            if (keyParts.Length != 2 || !int.TryParse(keyParts[1], out var accountId))
            {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("Invalid API key format");
                return;
            }

            var userKey = keyParts[0];

            try
            {
                // Find user by API key components
                var user = await dbContext.Users
                    .FirstOrDefaultAsync(u => u.ApiKey.StartsWith(userKey) && u.GameAccountId == accountId);

                if (user == null)
                {
                    context.Response.StatusCode = 401;
                    await context.Response.WriteAsync("Invalid API key");
                    return;
                }

                // Create claims for the authenticated user
                var claims = new List<Claim>
                {
                    new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new(ClaimTypes.Name, user.Name),
                    new("GameAccountId", user.GameAccountId.ToString()),
                    new("LocalContentId", user.PrimaryCharacterLocalContentId.ToString()),
                    new("AppRoleId", user.AppRoleId.ToString())
                };

                var identity = new ClaimsIdentity(claims, "ApiKey");
                context.User = new ClaimsPrincipal(identity);

                // Store user info in context for easy access
                context.Items["User"] = user;
                context.Items["UserId"] = user.Id;
                context.Items["GameAccountId"] = user.GameAccountId;

                // Update last login time
                user.LastLoginAt = DateTime.UtcNow;
                await dbContext.SaveChangesAsync();
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

    public static class ApiKeyAuthenticationMiddlewareExtensions
    {
        public static IApplicationBuilder UseApiKeyAuthentication(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<ApiKeyAuthenticationMiddleware>();
        }
    }
}