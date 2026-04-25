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

public class UsersControllerContributionsTests
{
    private static UsersController MakeController(MemoriaDbContext ctx, int? viewerUserId)
    {
        var c = new UsersController(ctx, NullLogger<UsersController>.Instance);
        var http = new DefaultHttpContext();
        http.Items["ViewerUserId"] = viewerUserId;
        c.ControllerContext = new ControllerContext { HttpContext = http };
        return c;
    }

    [Fact]
    public async Task GetContributions_Anonymous_Returns401()
    {
        using var ctx = new MemoriaDbContext(DatabaseTestUtilities.CreateInMemoryDbOptions<MemoriaDbContext>());
        var result = await MakeController(ctx, null).GetContributions();
        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetContributions_ReturnsLifetimeAndRecent()
    {
        using var ctx = new MemoriaDbContext(DatabaseTestUtilities.CreateInMemoryDbOptions<MemoriaDbContext>());
        ctx.Users.Add(new ApplicationUser { Id = 5, ApiKey = "x", DiscordUserId = 1, TotalContributions = 1234 });
        await ctx.SaveChangesAsync();

        var result = await MakeController(ctx, 5).GetContributions();
        var dto = ((OkObjectResult)result).Value.Should().BeOfType<ContributionsResponse>().Subject;
        dto.Lifetime.Should().Be(1234);
        dto.Recent.Should().BeEmpty();
    }

    [Fact]
    public async Task GetContributions_RecentOrderedByMostRecentScanWithWorldNames()
    {
        using var ctx = new MemoriaDbContext(DatabaseTestUtilities.CreateInMemoryDbOptions<MemoriaDbContext>());
        ctx.Users.Add(new ApplicationUser { Id = 5, ApiKey = "x", DiscordUserId = 1, TotalContributions = 0 });
        ctx.Players.AddRange(
            new Player { LocalContentId = 1, Name = "First",  HomeWorldId = 91 }, // Balmung
            new Player { LocalContentId = 2, Name = "Second", HomeWorldId = 65 }, // Midgardsormr
            new Player { LocalContentId = 3, Name = "Third",  HomeWorldId = 34 }  // Brynhildr
        );
        var now = DateTime.UtcNow;
        ctx.UserScannedPlayers.AddRange(
            new UserScannedPlayer { UserId = 5, PlayerLocalContentId = 1, LastScannedAt = now.AddMinutes(-3) },
            new UserScannedPlayer { UserId = 5, PlayerLocalContentId = 2, LastScannedAt = now.AddMinutes(-1) },
            new UserScannedPlayer { UserId = 5, PlayerLocalContentId = 3, LastScannedAt = now.AddMinutes(-2) }
        );
        await ctx.SaveChangesAsync();

        var result = await MakeController(ctx, 5).GetContributions();
        var dto = ((OkObjectResult)result).Value.Should().BeOfType<ContributionsResponse>().Subject;

        dto.Recent.Select(r => r.PlayerName).Should().Equal("Second", "Third", "First");
        dto.Recent.Select(r => r.WorldName).Should().Equal("Midgardsormr", "Brynhildr", "Balmung");
        dto.Recent.Select(r => r.WorldSlug).Should().Equal("midgardsormr", "brynhildr", "balmung");
    }

    [Fact]
    public async Task GetContributions_OnlyReturnsScansForViewer()
    {
        // Two users have each scanned different players; viewer should see only their own.
        using var ctx = new MemoriaDbContext(DatabaseTestUtilities.CreateInMemoryDbOptions<MemoriaDbContext>());
        ctx.Users.AddRange(
            new ApplicationUser { Id = 5, ApiKey = "viewer",  DiscordUserId = 1 },
            new ApplicationUser { Id = 6, ApiKey = "stranger", DiscordUserId = 2 }
        );
        ctx.Players.AddRange(
            new Player { LocalContentId = 100, Name = "MineOnly", HomeWorldId = 91 },
            new Player { LocalContentId = 200, Name = "TheirOnly", HomeWorldId = 91 }
        );
        ctx.UserScannedPlayers.AddRange(
            new UserScannedPlayer { UserId = 5, PlayerLocalContentId = 100, LastScannedAt = DateTime.UtcNow },
            new UserScannedPlayer { UserId = 6, PlayerLocalContentId = 200, LastScannedAt = DateTime.UtcNow }
        );
        await ctx.SaveChangesAsync();

        var result = await MakeController(ctx, 5).GetContributions();
        var dto = ((OkObjectResult)result).Value.Should().BeOfType<ContributionsResponse>().Subject;

        dto.Recent.Should().HaveCount(1);
        dto.Recent[0].PlayerName.Should().Be("MineOnly");
    }
}
