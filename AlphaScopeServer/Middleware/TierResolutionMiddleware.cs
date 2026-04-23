using AlphaScopeServer.Data;
using AlphaScopeServer.Models.Entities;

namespace AlphaScopeServer.Middleware
{
    /// <summary>
    /// Classifies the current request's viewer tier. Runs after ApiKeyAuthenticationMiddleware
    /// so context.Items["User"] is already populated.
    ///
    /// In Plan 0a this is a stub that returns Tier 1 for every request regardless of auth state,
    /// because no downstream code consumes Tier yet. The branching shape is complete and tested
    /// so Plan 0c only flips the one literal `ctx.Items["Tier"] = 1` to `tier` below.
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

            // NOTE (Plan 0a stub): Tier is unconditionally 1. Plan 0c replaces the literal below
            // with `tier` when the web app starts consuming it.
            ctx.Items["Tier"] = 1;
            ctx.Items["ViewerUserId"] = user?.Id;

            // Silence "unused" warning on `tier` — the branching must exist and be tested,
            // but the variable isn't written back to ctx.Items until Plan 0c flips the stub.
            _ = tier;

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
