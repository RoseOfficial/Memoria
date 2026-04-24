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

public class PlayersControllerRecentTests
{
    private static PlayersController MakeController(MemoriaDbContext ctx)
    {
        var c = new PlayersController(ctx, NullLogger<PlayersController>.Instance);
        c.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        return c;
    }

    [Fact]
    public async Task GetRecent_ReturnsItemsOrderedByLastScannedDesc()
    {
        using var ctx = new MemoriaDbContext(DatabaseTestUtilities.CreateInMemoryDbOptions<MemoriaDbContext>());
        ctx.Players.AddRange(
            new Player { LocalContentId = 1, Name = "A", HomeWorldId = 91, LastScannedAt = DateTime.UtcNow.AddMinutes(-10) },
            new Player { LocalContentId = 2, Name = "B", HomeWorldId = 91, LastScannedAt = DateTime.UtcNow.AddMinutes(-1) });
        await ctx.SaveChangesAsync();

        var result = await MakeController(ctx).GetRecent();
        var dto = ((OkObjectResult)result).Value.Should().BeOfType<RecentPlayerResponse>().Subject;
        dto.Items.Should().HaveCount(2);
        dto.Items[0].Name.Should().Be("B");
    }

    [Fact]
    public async Task GetRecent_FiltersHideEntirely()
    {
        using var ctx = new MemoriaDbContext(DatabaseTestUtilities.CreateInMemoryDbOptions<MemoriaDbContext>());
        ctx.Players.AddRange(
            new Player { LocalContentId = 1, Name = "A", HomeWorldId = 91, LastScannedAt = DateTime.UtcNow, HideEntirely = true },
            new Player { LocalContentId = 2, Name = "B", HomeWorldId = 91, LastScannedAt = DateTime.UtcNow });
        await ctx.SaveChangesAsync();

        var result = await MakeController(ctx).GetRecent();
        var dto = ((OkObjectResult)result).Value.Should().BeOfType<RecentPlayerResponse>().Subject;
        dto.Items.Should().ContainSingle().Which.Name.Should().Be("B");
    }

    [Fact]
    public async Task GetRecent_FiltersIsPrivateAndHideInSearch()
    {
        using var ctx = new MemoriaDbContext(DatabaseTestUtilities.CreateInMemoryDbOptions<MemoriaDbContext>());
        ctx.Players.AddRange(
            new Player { LocalContentId = 1, Name = "A", HomeWorldId = 91, LastScannedAt = DateTime.UtcNow, IsPrivate = true },
            new Player { LocalContentId = 2, Name = "B", HomeWorldId = 91, LastScannedAt = DateTime.UtcNow, HideInSearch = true },
            new Player { LocalContentId = 3, Name = "C", HomeWorldId = 91, LastScannedAt = DateTime.UtcNow });
        await ctx.SaveChangesAsync();

        var result = await MakeController(ctx).GetRecent();
        var dto = ((OkObjectResult)result).Value.Should().BeOfType<RecentPlayerResponse>().Subject;
        dto.Items.Should().ContainSingle().Which.Name.Should().Be("C");
    }

    [Fact]
    public async Task GetRecent_CapsAt20()
    {
        using var ctx = new MemoriaDbContext(DatabaseTestUtilities.CreateInMemoryDbOptions<MemoriaDbContext>());
        for (int i = 1; i <= 30; i++)
        {
            ctx.Players.Add(new Player { LocalContentId = i, Name = $"P{i}", HomeWorldId = 91, LastScannedAt = DateTime.UtcNow.AddSeconds(-i) });
        }
        await ctx.SaveChangesAsync();

        var result = await MakeController(ctx).GetRecent();
        var dto = ((OkObjectResult)result).Value.Should().BeOfType<RecentPlayerResponse>().Subject;
        dto.Items.Should().HaveCount(20);
    }
}
