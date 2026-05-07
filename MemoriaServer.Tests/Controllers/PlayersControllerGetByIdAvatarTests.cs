using MemoriaServer.Controllers;
using MemoriaServer.Data;
using MemoriaServer.Models.DTOs;
using MemoriaServer.Models.Entities;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using TestUtilities;
using Xunit;

namespace MemoriaServer.Tests.Controllers;

// Locks in the avatar coalesce in GetPlayerById. Before the fix, the response only
// surfaced AvatarLink when a PlayerLodestone navigation row existed — but enrichment
// only writes that row when match.Id parses to an int. The plugin's BackfillAvatarsLoop
// reads PlayerLodestone?.AvatarLink, so without the fallback it would never see the
// avatar that was sitting in Player.AvatarLink (the column the SearchPlayers endpoint
// reads from).
public class PlayersControllerGetByIdAvatarTests
{
    private static PlayersController MakeController(MemoriaDbContext ctx)
    {
        var c = new PlayersController(ctx, NullLogger<PlayersController>.Instance);
        c.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        return c;
    }

    [Fact]
    public async Task GetPlayerById_SurfacesAvatarLink_WhenOnlyPlayerColumnIsSet()
    {
        using var ctx = new MemoriaDbContext(DatabaseTestUtilities.CreateInMemoryDbOptions<MemoriaDbContext>());
        ctx.Players.Add(new Player
        {
            LocalContentId = 555,
            Name = "Avatar Sole",
            HomeWorldId = 91,
            AvatarLink = "https://img2.finalfantasyxiv.com/f/abc_96x96.jpg",
        });
        await ctx.SaveChangesAsync();

        var result = await MakeController(ctx).GetPlayerById(555);
        var dto = ((OkObjectResult)result.Result!).Value.Should().BeOfType<PlayerDetailed>().Subject;

        dto.PlayerLodestone.Should().NotBeNull("the controller must synthesize the DTO so the plugin's backfill can read AvatarLink");
        dto.PlayerLodestone!.AvatarLink.Should().Be("https://img2.finalfantasyxiv.com/f/abc_96x96.jpg");
        dto.PlayerLodestone.LodestoneId.Should().BeNull();
    }

    [Fact]
    public async Task GetPlayerById_PrefersLodestoneAvatarOverPlayerColumn_WhenBothPresent()
    {
        using var ctx = new MemoriaDbContext(DatabaseTestUtilities.CreateInMemoryDbOptions<MemoriaDbContext>());
        var player = new Player
        {
            LocalContentId = 556,
            Name = "Both Set",
            HomeWorldId = 91,
            AvatarLink = "https://img2.finalfantasyxiv.com/f/player-column.jpg",
        };
        ctx.Players.Add(player);
        ctx.PlayerLodestones.Add(new PlayerLodestone
        {
            PlayerLocalContentId = 556,
            LodestoneId = 12345,
            AvatarLink = "https://img2.finalfantasyxiv.com/f/lodestone-row.jpg",
        });
        await ctx.SaveChangesAsync();

        var result = await MakeController(ctx).GetPlayerById(556);
        var dto = ((OkObjectResult)result.Result!).Value.Should().BeOfType<PlayerDetailed>().Subject;

        dto.PlayerLodestone.Should().NotBeNull();
        dto.PlayerLodestone!.AvatarLink.Should().Be("https://img2.finalfantasyxiv.com/f/lodestone-row.jpg",
            "the navigation row is the canonical source when present");
        dto.PlayerLodestone.LodestoneId.Should().Be(12345);
    }

    [Fact]
    public async Task GetPlayerById_LeavesPlayerLodestoneNull_WhenNoAvatarAnywhere()
    {
        using var ctx = new MemoriaDbContext(DatabaseTestUtilities.CreateInMemoryDbOptions<MemoriaDbContext>());
        ctx.Players.Add(new Player
        {
            LocalContentId = 557,
            Name = "Never Enriched",
            HomeWorldId = 91,
            // AvatarLink is null and no PlayerLodestone row exists
        });
        await ctx.SaveChangesAsync();

        var result = await MakeController(ctx).GetPlayerById(557);
        var dto = ((OkObjectResult)result.Result!).Value.Should().BeOfType<PlayerDetailed>().Subject;

        dto.PlayerLodestone.Should().BeNull("nothing to surface — the plugin's cooldown will gate the next re-ask");
    }
}
