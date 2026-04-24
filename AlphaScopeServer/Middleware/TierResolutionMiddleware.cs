using AlphaScopeServer.Data;
using AlphaScopeServer.Models.Entities;
using AlphaScopeServer.Services.Admin;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AlphaScopeServer.Middleware
{
    /// <summary>
    /// Classifies the current request's viewer tier. Runs after ApiKeyAuthenticationMiddleware
    /// so context.Items["User"] is already populated.
    ///
    /// Plan 0c flipped the one literal `ctx.Items["Tier"] = 1` to use the computed tier, so
    /// guild-member viewers receive Tier 2 content end-to-end.
    /// </summary>
    public class TierResolutionMiddleware
    {
        private readonly RequestDelegate _next;

        public TierResolutionMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext ctx, AlphaScopeDbContext db)
        {
            var user = ctx.Items["User"] as ApplicationUser;

            int tier;
            if (user is null)
            {
                tier = 1;
            }
            else if (await IsGuildMemberFresh(user, db))
            {
                tier = 2;
            }
            else
            {
                tier = 1;
            }

            ctx.Items["Tier"] = tier;
            ctx.Items["ViewerUserId"] = user?.Id;

            bool isAdmin = false;
            if (user?.DiscordUserId is { } did)
            {
                var adminOptions = ctx.RequestServices.GetService<IOptions<AdminOptions>>()?.Value;
                isAdmin = adminOptions?.DiscordUserIds.Contains(did) ?? false;
            }
            ctx.Items["IsAdmin"] = isAdmin;

            await _next(ctx);
        }

        /// <summary>
        /// Stub in Plan 0a — always returns false. Plan 0c replaces the body with a Discord
        /// guild-membership fetch cached for 24h on the ApplicationUser row. Made static
        /// so tests can exercise it without constructing the middleware.
        /// </summary>
        public static Task<bool> IsGuildMemberFresh(ApplicationUser user, AlphaScopeDbContext db)
        {
            if (user.GuildMembershipCheckedAt is null) return Task.FromResult(false);
            if (DateTime.UtcNow - user.GuildMembershipCheckedAt.Value > TimeSpan.FromHours(24))
                return Task.FromResult(false);
            // Plan 0a: always false. Plan 0c: return user.IsGuildMember after optional refresh.
            return Task.FromResult(false);
        }
    }

    public static class TierResolutionMiddlewareExtensions
    {
        public static IApplicationBuilder UseTierResolution(this IApplicationBuilder builder)
            => builder.UseMiddleware<TierResolutionMiddleware>();
    }
}
