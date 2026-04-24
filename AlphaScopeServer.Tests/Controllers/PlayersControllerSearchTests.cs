using AlphaScopeServer.Controllers;
using AlphaScopeServer.Data;
using AlphaScopeServer.Models.DTOs;
using AlphaScopeServer.Models.Entities;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using TestUtilities;
using Xunit;

namespace AlphaScopeServer.Tests.Controllers;

public class PlayersControllerSearchTests
{
    private static PlayersController MakeController(AlphaScopeDbContext ctx)
    {
        var c = new PlayersController(ctx, NullLogger<PlayersController>.Instance);
        c.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        return c;
    }

    [Fact]
    public async Task Search_EmptyQuery_ReturnsEmpty()
    {
        using var ctx = new AlphaScopeDbContext(DatabaseTestUtilities.CreateInMemoryDbOptions<AlphaScopeDbContext>());
        ctx.Players.Add(new Player { LocalContentId = 1, Name = "Tataru Taru", HomeWorldId = 91 });
        await ctx.SaveChangesAsync();

        var result = await MakeController(ctx).Search("");
        var dto = ((OkObjectResult)result).Value.Should().BeOfType<PlayerSearchResultResponse>().Subject;
        dto.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Search_MatchesPartialName()
    {
        using var ctx = new AlphaScopeDbContext(DatabaseTestUtilities.CreateInMemoryDbOptions<AlphaScopeDbContext>());
        ctx.Players.AddRange(
            new Player { LocalContentId = 1, Name = "Tataru Taru", HomeWorldId = 91 },
            new Player { LocalContentId = 2, Name = "Unrelated", HomeWorldId = 91 });
        await ctx.SaveChangesAsync();

        var result = await MakeController(ctx).Search("Tataru");
        var dto = ((OkObjectResult)result).Value.Should().BeOfType<PlayerSearchResultResponse>().Subject;
        dto.Items.Should().ContainSingle().Which.Name.Should().Be("Tataru Taru");
    }

    [Fact]
    public async Task Search_PopulatesWorldSlugAndName()
    {
        using var ctx = new AlphaScopeDbContext(DatabaseTestUtilities.CreateInMemoryDbOptions<AlphaScopeDbContext>());
        ctx.Players.Add(new Player { LocalContentId = 1, Name = "Tataru Taru", HomeWorldId = 91 });
        await ctx.SaveChangesAsync();

        var result = await MakeController(ctx).Search("Tataru");
        var dto = ((OkObjectResult)result).Value.Should().BeOfType<PlayerSearchResultResponse>().Subject;
        dto.Items[0].WorldSlug.Should().Be("balmung");
        dto.Items[0].WorldName.Should().Be("Balmung");
    }

    [Fact]
    public async Task Search_FiltersHideEntirelyAndIsPrivateAndHideInSearch()
    {
        using var ctx = new AlphaScopeDbContext(DatabaseTestUtilities.CreateInMemoryDbOptions<AlphaScopeDbContext>());
        ctx.Players.AddRange(
            new Player { LocalContentId = 1, Name = "Alice", HomeWorldId = 91, HideEntirely = true },
            new Player { LocalContentId = 2, Name = "Alice Too", HomeWorldId = 91, IsPrivate = true },
            new Player { LocalContentId = 3, Name = "Alice Three", HomeWorldId = 91, HideInSearch = true },
            new Player { LocalContentId = 4, Name = "Alice Four", HomeWorldId = 91 });
        await ctx.SaveChangesAsync();

        var result = await MakeController(ctx).Search("Alice");
        var dto = ((OkObjectResult)result).Value.Should().BeOfType<PlayerSearchResultResponse>().Subject;
        dto.Items.Should().ContainSingle().Which.Name.Should().Be("Alice Four");
    }

    [Fact]
    public async Task Search_CapsAt50()
    {
        using var ctx = new AlphaScopeDbContext(DatabaseTestUtilities.CreateInMemoryDbOptions<AlphaScopeDbContext>());
        for (int i = 1; i <= 60; i++)
        {
            ctx.Players.Add(new Player { LocalContentId = i, Name = $"Alice {i:D2}", HomeWorldId = 91 });
        }
        await ctx.SaveChangesAsync();

        var result = await MakeController(ctx).Search("Alice");
        var dto = ((OkObjectResult)result).Value.Should().BeOfType<PlayerSearchResultResponse>().Subject;
        dto.Items.Should().HaveCount(50);
    }
}
