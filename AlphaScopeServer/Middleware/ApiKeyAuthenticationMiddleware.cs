using Microsoft.EntityFrameworkCore;
using AlphaScopeServer.Data;
using System.Security.Claims;

namespace AlphaScopeServer.Middleware
{
    /// <summary>
    /// Custom authentication middleware for AlphaScope API key validation.
    /// Handles authentication using the {UserKey}-{AccountId} format API keys,
    /// validates users against the database, creates claims-based identity,
    /// and provides user context for downstream request processing.
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

                // Update last login time only if it's been more than 5 minutes since last update
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