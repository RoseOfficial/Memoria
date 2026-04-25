using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MemoriaServer.Data;
using MemoriaServer.Models.Entities;
using MemoriaServer.Services.Lodestone;
using MemoriaServer.Tests.Infrastructure;
using MemoriaServer.Tests.TestDoubles;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MemoriaServer.Tests.Controllers;

/// <summary>
/// End-to-end test for the character-claim flow: api-key middleware →
/// PlayersController.StartClaim → user pastes code in Lodestone bio (faked) →
/// PlayersController.VerifyClaim → Player.ClaimedByUserId set. The unit-level
/// PlayersClaimTests use the direct-controller pattern; this exercises the full
/// pipeline so a regression in middleware/route wiring would surface here.
/// </summary>
public class ClaimFlowIntegrationTests
{
    [Fact]
    public async Task FullClaimFlow_StartReturnsCode_VerifyClaimsPlayerWithBioMatch()
    {
        // Bios dict is shared between the test (write) and the fake fetcher (read);
        // FakeLodestoneBioFetcher stores a reference to the same Dictionary instance,
        // so mutating it after registration is what lets the test simulate the user
        // editing their Lodestone bio between claim/start and claim/verify.
        var bios = new Dictionary<int, string?>();
        var lodestoneId = 90210;
        const long localContentId = 7777_8888_9999_1111L;

        var factory = new TestAppFactory().WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<ILodestoneBioFetcher>(new FakeLodestoneBioFetcher(bios));
            });
        });

        // Seed an authenticated user, a Player to claim, and a Lodestone link so
        // start/verify can resolve the LodestoneId for the bio fetch.
        using (var scope = factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<MemoriaDbContext>();
            ctx.Users.Add(new ApplicationUser
            {
                Name = "Claimant",
                ApiKey = "CLAIMANT-INT-KEY",
                PrimaryCharacterLocalContentId = localContentId,
            });
            ctx.Players.Add(new Player
            {
                LocalContentId = localContentId,
                Name = "TargetCharacter",
                HomeWorldId = 91,
            });
            await ctx.SaveChangesAsync();
            ctx.PlayerLodestones.Add(new PlayerLodestone
            {
                PlayerLocalContentId = localContentId,
                LodestoneId = lodestoneId,
            });
            await ctx.SaveChangesAsync();
        }

        var client = factory.CreateClient();

        // 1. claim/start — middleware authenticates via api-key, controller mints a code.
        var startReq = new HttpRequestMessage(HttpMethod.Post, $"/v1/players/{localContentId}/claim/start");
        startReq.Headers.Add("api-key", "CLAIMANT-INT-KEY");
        var startRes = await client.SendAsync(startReq);
        startRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var startBody = await startRes.Content.ReadFromJsonAsync<ClaimStartDto>();
        startBody.Should().NotBeNull();
        startBody!.code.Should().StartWith("AS-");

        // 2. Simulate the user pasting the code into their Lodestone bio.
        bios[lodestoneId] = $"Greetings traveller! verification: {startBody.code}";

        // 3. claim/verify — fetcher returns the bio, controller matches, claim sticks.
        var verifyReq = new HttpRequestMessage(HttpMethod.Post, $"/v1/players/{localContentId}/claim/verify");
        verifyReq.Headers.Add("api-key", "CLAIMANT-INT-KEY");
        var verifyRes = await client.SendAsync(verifyReq);
        verifyRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var verifyBody = await verifyRes.Content.ReadFromJsonAsync<ClaimVerifyDto>();
        verifyBody!.claimed.Should().BeTrue();

        // DB state: Player is claimed by the authenticated user; ClaimAttempt is consumed.
        using (var scope = factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<MemoriaDbContext>();
            var user = await ctx.Users.AsNoTracking().FirstAsync();
            var player = await ctx.Players.AsNoTracking().FirstAsync(p => p.LocalContentId == localContentId);
            player.ClaimedByUserId.Should().Be(user.Id);
            player.ClaimedAt.Should().NotBeNull();
            player.ClaimVerifiedAt.Should().NotBeNull();
            (await ctx.ClaimAttempts.CountAsync()).Should().Be(0, "verify consumes the attempt");
        }
    }

    [Fact]
    public async Task Verify_WithoutMatchingBio_DoesNotClaim()
    {
        var bios = new Dictionary<int, string?>();
        var lodestoneId = 11111;
        const long localContentId = 4242L;

        var factory = new TestAppFactory().WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<ILodestoneBioFetcher>(new FakeLodestoneBioFetcher(bios));
            });
        });

        using (var scope = factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<MemoriaDbContext>();
            ctx.Users.Add(new ApplicationUser { Name = "U", ApiKey = "MISMATCH-KEY", PrimaryCharacterLocalContentId = 0 });
            ctx.Players.Add(new Player { LocalContentId = localContentId, Name = "T", HomeWorldId = 91 });
            await ctx.SaveChangesAsync();
            ctx.PlayerLodestones.Add(new PlayerLodestone { PlayerLocalContentId = localContentId, LodestoneId = lodestoneId });
            await ctx.SaveChangesAsync();
        }

        var client = factory.CreateClient();

        var startReq = new HttpRequestMessage(HttpMethod.Post, $"/v1/players/{localContentId}/claim/start");
        startReq.Headers.Add("api-key", "MISMATCH-KEY");
        (await client.SendAsync(startReq)).EnsureSuccessStatusCode();

        // Bio is set but does NOT contain the start code.
        bios[lodestoneId] = "this bio is missing the verification code entirely";

        var verifyReq = new HttpRequestMessage(HttpMethod.Post, $"/v1/players/{localContentId}/claim/verify");
        verifyReq.Headers.Add("api-key", "MISMATCH-KEY");
        var verifyRes = await client.SendAsync(verifyReq);

        verifyRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using (var scope = factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<MemoriaDbContext>();
            var player = await ctx.Players.AsNoTracking().FirstAsync(p => p.LocalContentId == localContentId);
            player.ClaimedByUserId.Should().BeNull();
            // The attempt is incremented but kept around so the user can retry up to 5 times.
            var attempt = await ctx.ClaimAttempts.AsNoTracking().FirstAsync(a => a.PlayerLocalContentId == localContentId);
            attempt.Attempts.Should().Be(1);
        }
    }

    private sealed record ClaimStartDto(string code, DateTime expiresAt, string instructions);
    private sealed record ClaimVerifyDto(bool claimed, string characterName, short? homeWorldId);
}
