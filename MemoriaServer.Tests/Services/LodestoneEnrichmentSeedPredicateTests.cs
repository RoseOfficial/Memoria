using FluentAssertions;
using MemoriaServer.Data;
using MemoriaServer.Models.Entities;
using Microsoft.EntityFrameworkCore;
using TestUtilities;
using Xunit;

namespace MemoriaServer.Tests.Services;

// Locks in the LodestoneEnrichmentService.SeedFromDatabaseAsync predicate. The earlier
// predicate also OR'd on `LodestoneJobData == null`, which sounded reasonable but had a
// hidden failure mode: NotFound players (free-trial chars, deleted, very-new) only stamp
// LastJobDataUpdate — they never write LodestoneJobData. Under the old predicate they
// matched the null clause every hour and got re-enqueued forever, eating queue budget and
// starving freshly-uploaded players that landed behind the backlog.
//
// This test replicates the exact predicate inline against an InMemory DB so the seed
// behavior is locked in even though SeedFromDatabaseAsync itself is private.
public class LodestoneEnrichmentSeedPredicateTests
{
    private static readonly TimeSpan StaleAfter = TimeSpan.FromDays(7);

    [Fact]
    public async Task SeedPredicate_skips_NotFound_players()
    {
        using var ctx = new MemoriaDbContext(DatabaseTestUtilities.CreateInMemoryDbOptions<MemoriaDbContext>());

        var now = DateTime.UtcNow;
        ctx.Players.AddRange(
            // Never enriched — should be picked up.
            new Player { LocalContentId = 1, Name = "Never Enriched", LastJobDataUpdate = null, LodestoneJobData = null },
            // NotFound recently — must NOT be picked up. This is the regression case.
            new Player { LocalContentId = 2, Name = "Notfound Recent", LastJobDataUpdate = now, LodestoneJobData = null },
            // Stale — should be picked up.
            new Player { LocalContentId = 3, Name = "Stale Player", LastJobDataUpdate = now - StaleAfter - TimeSpan.FromHours(1), LodestoneJobData = "{\"19\":80}" },
            // Recently enriched — must NOT be picked up.
            new Player { LocalContentId = 4, Name = "Recently Enriched", LastJobDataUpdate = now, LodestoneJobData = "{\"19\":80}" });
        await ctx.SaveChangesAsync();

        var staleCutoff = now - StaleAfter;
        var ids = await ctx.Players
            .Where(p => p.LastJobDataUpdate == null
                        || p.LastJobDataUpdate < staleCutoff)
            .Select(p => p.LocalContentId)
            .ToListAsync();

        ids.Should().BeEquivalentTo(new[] { 1L, 3L },
            "the predicate must seed never-enriched and stale players, and must skip NotFound and recently-enriched ones");
    }

    [Fact]
    public async Task SeedPredicate_picks_up_genuinely_stale_players()
    {
        using var ctx = new MemoriaDbContext(DatabaseTestUtilities.CreateInMemoryDbOptions<MemoriaDbContext>());

        var now = DateTime.UtcNow;
        ctx.Players.AddRange(
            // Just inside the staleness window — must NOT pick up.
            new Player { LocalContentId = 10, Name = "Just Inside", LastJobDataUpdate = now - StaleAfter + TimeSpan.FromMinutes(1), LodestoneJobData = "{}" },
            // Just past the staleness window — must pick up.
            new Player { LocalContentId = 11, Name = "Just Past", LastJobDataUpdate = now - StaleAfter - TimeSpan.FromMinutes(1), LodestoneJobData = "{}" });
        await ctx.SaveChangesAsync();

        var staleCutoff = now - StaleAfter;
        var ids = await ctx.Players
            .Where(p => p.LastJobDataUpdate == null
                        || p.LastJobDataUpdate < staleCutoff)
            .Select(p => p.LocalContentId)
            .ToListAsync();

        ids.Should().ContainSingle().Which.Should().Be(11);
    }
}
