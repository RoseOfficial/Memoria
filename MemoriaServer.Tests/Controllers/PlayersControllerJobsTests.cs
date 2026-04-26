using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MemoriaServer.Data;
using MemoriaServer.Models.DTOs;
using MemoriaServer.Models.Entities;
using MemoriaServer.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MemoriaServer.Tests.Controllers;

/// <summary>
/// End-to-end tests for the Jobs section returned by GET /v1/players/by-slug.
/// The plugin's Lodestone scrape ships LodestoneJobData as a JSON dict keyed by
/// stringified ClassJob ids (e.g. {"19":82}); BuildJobs has to resolve those to
/// names AND drop redundant starter-class entries when the upgrade job is also
/// present (Lodestone reports both at the same level).
/// </summary>
public class PlayersControllerJobsTests
{
    [Fact]
    public async Task BySlug_ResolvesNumericKeysToJobNames()
    {
        var factory = new TestAppFactory();
        using (var scope = factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<MemoriaDbContext>();
            ctx.Players.Add(new Player
            {
                LocalContentId = 555,
                Name = "JobTester",
                HomeWorldId = 91, // Balmung
                LodestoneJobData = """{"19":82,"23":31,"32":70,"42":80}""",
            });
            await ctx.SaveChangesAsync();
        }

        var client = factory.CreateClient();
        var res = await client.GetAsync("/v1/players/by-slug?world=balmung&name=jobtester");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var profile = await res.Content.ReadFromJsonAsync<PlayerProfileResponse>();

        var names = profile!.Jobs.Jobs.Select(j => j.Name).ToList();
        names.Should().BeEquivalentTo(new[] { "Paladin", "Bard", "Dark Knight", "Pictomancer" });
    }

    [Fact]
    public async Task BySlug_DropsStarterClassWhenUpgradeJobPresent()
    {
        var factory = new TestAppFactory();
        using (var scope = factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<MemoriaDbContext>();
            ctx.Players.Add(new Player
            {
                LocalContentId = 556,
                Name = "DupeTester",
                HomeWorldId = 91,
                // Lodestone always reports both class AND its upgrade job at identical
                // levels post-30. The dedupe drops the class entry. Arcanist branches
                // into two jobs (SMN + SCH) so we cover that special case here too.
                LodestoneJobData = """{"1":82,"19":82,"3":70,"21":70,"26":100,"27":100,"28":100,"6":100,"24":100}""",
            });
            await ctx.SaveChangesAsync();
        }

        var client = factory.CreateClient();
        var res = await client.GetAsync("/v1/players/by-slug?world=balmung&name=dupetester");
        var profile = await res.Content.ReadFromJsonAsync<PlayerProfileResponse>();

        var names = profile!.Jobs.Jobs.Select(j => j.Name).ToList();
        names.Should().NotContain(new[] { "Gladiator", "Marauder", "Arcanist", "Conjurer" });
        names.Should().BeEquivalentTo(new[] { "Paladin", "Warrior", "Summoner", "Scholar", "White Mage" });
    }

    [Fact]
    public async Task BySlug_KeepsStarterClassWhenUpgradeJobAbsent()
    {
        var factory = new TestAppFactory();
        using (var scope = factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<MemoriaDbContext>();
            ctx.Players.Add(new Player
            {
                LocalContentId = 557,
                Name = "PreThirty",
                HomeWorldId = 91,
                // Pre-30 character on Gladiator only — no Paladin upgrade unlocked yet.
                // Class entry must survive the dedupe pass or the Jobs panel goes empty.
                LodestoneJobData = """{"1":15,"4":10}""",
            });
            await ctx.SaveChangesAsync();
        }

        var client = factory.CreateClient();
        var res = await client.GetAsync("/v1/players/by-slug?world=balmung&name=prethirty");
        var profile = await res.Content.ReadFromJsonAsync<PlayerProfileResponse>();

        var names = profile!.Jobs.Jobs.Select(j => j.Name).ToList();
        names.Should().BeEquivalentTo(new[] { "Gladiator", "Lancer" });
    }

    [Fact]
    public async Task BySlug_OrdersJobsByLevelDescending()
    {
        var factory = new TestAppFactory();
        using (var scope = factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<MemoriaDbContext>();
            ctx.Players.Add(new Player
            {
                LocalContentId = 558,
                Name = "SortTester",
                HomeWorldId = 91,
                LodestoneJobData = """{"19":50,"23":100,"32":75,"42":1}""",
            });
            await ctx.SaveChangesAsync();
        }

        var client = factory.CreateClient();
        var res = await client.GetAsync("/v1/players/by-slug?world=balmung&name=sorttester");
        var profile = await res.Content.ReadFromJsonAsync<PlayerProfileResponse>();

        var levels = profile!.Jobs.Jobs.Select(j => j.Level).ToList();
        levels.Should().BeInDescendingOrder();
        profile.Jobs.Jobs[0].Name.Should().Be("Bard");
        profile.Jobs.Jobs[0].Level.Should().Be(100);
    }
}
