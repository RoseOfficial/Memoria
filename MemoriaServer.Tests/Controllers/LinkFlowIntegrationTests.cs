using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MemoriaServer.Data;
using MemoriaServer.Models.Entities;
using MemoriaServer.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MemoriaServer.Tests.Controllers;

/// <summary>
/// End-to-end test for the plugin↔web account-link flow. Exercises the full
/// pipeline: ApiKeyAuthenticationMiddleware (api-key header for generate, cookie
/// for redeem), AuthController routes, and the merge logic that folds the web
/// user into the plugin user. The unit-level tests in AuthControllerTests use
/// the direct-controller pattern and bypass the middleware, so they don't catch
/// auth-pipeline regressions like the /auth/* bypass bug we fixed earlier today.
/// </summary>
public class LinkFlowIntegrationTests
{
    [Fact]
    public async Task GenerateThenRedeem_MergesWebUserIntoPluginUser_AndReissuesCookie()
    {
        var factory = new AuthControllerOAuthFactory();

        // Seed the two distinct rows that represent the same person before the merge:
        //   pluginUser  — what the plugin authenticates as via api-key
        //   webUser     — what the Discord OAuth callback issued via cookie
        int pluginUserId, webUserId;
        using (var scope = factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<MemoriaDbContext>();
            var plugin = new ApplicationUser
            {
                Name = "PluginIdentity",
                ApiKey = "PLUGIN-INT-KEY",
                PrimaryCharacterLocalContentId = 12345,
                GameAccountId = 999_888,
                TotalContributions = 42,
            };
            var web = new ApplicationUser
            {
                Name = "WebIdentity",
                ApiKey = "WEB-INT-KEY",
                PrimaryCharacterLocalContentId = 0,
                DiscordUserId = 5555,
                IsGuildMember = true,
                GuildMembershipCheckedAt = DateTime.UtcNow,
            };
            ctx.Users.AddRange(plugin, web);
            await ctx.SaveChangesAsync();
            pluginUserId = plugin.Id;
            webUserId = web.Id;
        }

        var client = factory.CreateClient(new() { AllowAutoRedirect = false });

        // 1. Plugin generates a link code (api-key auth).
        var genReq = new HttpRequestMessage(HttpMethod.Post, "/v1/auth/link/generate");
        genReq.Headers.Add("api-key", "PLUGIN-INT-KEY");
        var genRes = await client.SendAsync(genReq);
        genRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var genBody = await genRes.Content.ReadFromJsonAsync<LinkGenerateResponseDto>();
        genBody.Should().NotBeNull();
        genBody!.code.Should().StartWith("AL-");

        // 2. Web user redeems the code (cookie auth).
        var redeemReq = new HttpRequestMessage(HttpMethod.Post, "/v1/auth/link/redeem");
        redeemReq.Headers.Add("Cookie", "__Host-memoria=WEB-INT-KEY");
        redeemReq.Content = JsonContent.Create(new { code = genBody.code });
        var redeemRes = await client.SendAsync(redeemReq);
        redeemRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // 3. The reissued cookie now points at the plugin user's api-key, so subsequent
        //    web requests authenticate as the merged identity.
        redeemRes.Headers.TryGetValues("Set-Cookie", out var setCookies).Should().BeTrue();
        setCookies!.Should().Contain(c => c.StartsWith("__Host-memoria=PLUGIN-INT-KEY"));

        // 4. DB state: webUser is gone, pluginUser kept its plugin identity AND inherited
        //    the web user's Discord identity. TotalContributions survives the merge.
        using (var scope = factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<MemoriaDbContext>();
            var users = await ctx.Users.AsNoTracking().ToListAsync();
            users.Should().HaveCount(1, "the web user row should be deleted by the merge");
            var merged = users[0];
            merged.Id.Should().Be(pluginUserId);
            merged.ApiKey.Should().Be("PLUGIN-INT-KEY");
            merged.DiscordUserId.Should().Be(5555);
            merged.IsGuildMember.Should().BeTrue();
            merged.TotalContributions.Should().Be(42, "merge must preserve plugin-side scan history");
        }
    }

    [Fact]
    public async Task Redeem_WithExpiredCode_Returns410Gone()
    {
        var factory = new AuthControllerOAuthFactory();

        using (var scope = factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<MemoriaDbContext>();
            var plugin = new ApplicationUser { Name = "P", ApiKey = "PLUG-EXP", PrimaryCharacterLocalContentId = 1 };
            var web = new ApplicationUser { Name = "W", ApiKey = "WEB-EXP", PrimaryCharacterLocalContentId = 0, DiscordUserId = 7777 };
            ctx.Users.AddRange(plugin, web);
            await ctx.SaveChangesAsync();
            ctx.AccountLinkCodes.Add(new AccountLinkCode
            {
                ApplicationUserId = plugin.Id,
                Code = "AL-EXPIRED-FOR-INTEGRATION",
                ExpiresAt = DateTime.UtcNow.AddMinutes(-1),
            });
            await ctx.SaveChangesAsync();
        }

        var client = factory.CreateClient();
        var req = new HttpRequestMessage(HttpMethod.Post, "/v1/auth/link/redeem");
        req.Headers.Add("Cookie", "__Host-memoria=WEB-EXP");
        req.Content = JsonContent.Create(new { code = "AL-EXPIRED-FOR-INTEGRATION" });
        var res = await client.SendAsync(req);

        res.StatusCode.Should().Be(HttpStatusCode.Gone);
    }

    private sealed record LinkGenerateResponseDto(string code, DateTime expiresAt);
}
