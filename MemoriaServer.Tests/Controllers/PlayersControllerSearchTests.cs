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

public class PlayersControllerSearchTests
{
    private static PlayersController MakeController(MemoriaDbContext ctx)
    {
        var c = new PlayersController(ctx, NullLogger<PlayersController>.Instance);
        c.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        return c;
    }

    [Fact]
    public async Task Search_EmptyQuery_ReturnsEmpty()
    {
        using var ctx = new MemoriaDbContext(DatabaseTestUtilities.CreateInMemoryDbOptions<MemoriaDbContext>());
        ctx.Players.Add(new Player { LocalContentId = 1, Name = "Tataru Taru", HomeWorldId = 91 });
        await ctx.SaveChangesAsync();

        var result = await MakeController(ctx).Search("");
        var dto = ((OkObjectResult)result).Value.Should().BeOfType<PlayerSearchResultResponse>().Subject;
        dto.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Search_MatchesPartialName()
    {
        using var ctx = new MemoriaDbContext(DatabaseTestUtilities.CreateInMemoryDbOptions<MemoriaDbContext>());
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
        using var ctx = new MemoriaDbContext(DatabaseTestUtilities.CreateInMemoryDbOptions<MemoriaDbContext>());
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
        using var ctx = new MemoriaDbContext(DatabaseTestUtilities.CreateInMemoryDbOptions<MemoriaDbContext>());
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
        using var ctx = new MemoriaDbContext(DatabaseTestUtilities.CreateInMemoryDbOptions<MemoriaDbContext>());
        for (int i = 1; i <= 60; i++)
        {
            ctx.Players.Add(new Player { LocalContentId = i, Name = $"Alice {i:D2}", HomeWorldId = 91 });
        }
        await ctx.SaveChangesAsync();

        var result = await MakeController(ctx).Search("Alice");
        var dto = ((OkObjectResult)result).Value.Should().BeOfType<PlayerSearchResultResponse>().Subject;
        dto.Items.Should().HaveCount(50);
    }

    [Fact]
    public async Task Search_LimitParameter_TruncatesResults()
    {
        using var ctx = new MemoriaDbContext(DatabaseTestUtilities.CreateInMemoryDbOptions<MemoriaDbContext>());
        for (int i = 1; i <= 30; i++)
            ctx.Players.Add(new Player { LocalContentId = i, Name = $"Alice {i:D2}", HomeWorldId = 91 });
        await ctx.SaveChangesAsync();

        var result = await MakeController(ctx).Search("Alice", limit: 10);
        var dto = ((OkObjectResult)result).Value.Should().BeOfType<PlayerSearchResultResponse>().Subject;
        dto.Items.Should().HaveCount(10);
    }

    [Fact]
    public async Task Search_IsCaseInsensitive()
    {
        using var ctx = new MemoriaDbContext(DatabaseTestUtilities.CreateInMemoryDbOptions<MemoriaDbContext>());
        ctx.Players.Add(new Player { LocalContentId = 1, Name = "Rose Ultima", HomeWorldId = 91 });
        await ctx.SaveChangesAsync();

        var lower = await MakeController(ctx).Search("rose");
        var upper = await MakeController(ctx).Search("ROSE");
        var mixed = await MakeController(ctx).Search("RoSe");

        ((PlayerSearchResultResponse)((OkObjectResult)lower).Value!).Items.Should().ContainSingle();
        ((PlayerSearchResultResponse)((OkObjectResult)upper).Value!).Items.Should().ContainSingle();
        ((PlayerSearchResultResponse)((OkObjectResult)mixed).Value!).Items.Should().ContainSingle();
    }

    [Fact]
    public async Task Search_RanksPrefixBeforeWordPrefixBeforeSubstring()
    {
        // Three players, three score buckets:
        //   "Rose Ultima"          → starts-with "rose" → score 0 (best)
        //   "Lehlei Rose"          → word-prefix " Rose" → score 1
        //   "Bellrose Apostle"     → embedded substring  → score 2 (worst)
        // Expected order on a "rose" query is exactly that.
        using var ctx = new MemoriaDbContext(DatabaseTestUtilities.CreateInMemoryDbOptions<MemoriaDbContext>());
        ctx.Players.AddRange(
            new Player { LocalContentId = 3, Name = "Bellrose Apostle", HomeWorldId = 91 },
            new Player { LocalContentId = 2, Name = "Lehlei Rose",     HomeWorldId = 91 },
            new Player { LocalContentId = 1, Name = "Rose Ultima",     HomeWorldId = 91 }
        );
        await ctx.SaveChangesAsync();

        var result = await MakeController(ctx).Search("rose");
        var dto = ((OkObjectResult)result).Value.Should().BeOfType<PlayerSearchResultResponse>().Subject;
        dto.Items.Select(i => i.Name).Should().Equal("Rose Ultima", "Lehlei Rose", "Bellrose Apostle");
    }

    [Theory]
    [InlineData("Rose Ultima", "rose", 0)]      // prefix
    [InlineData("Rose Ultima", "ros", 0)]       // partial prefix
    [InlineData("Lehlei Rose", "rose", 1)]      // word-prefix on second word
    [InlineData("Bellrose Apostle", "rose", 2)] // embedded substring
    [InlineData("ROSE ultima", "rose", 0)]      // case-insensitive prefix
    public void ScoreNameMatch_AssignsExpectedBucket(string name, string needle, int expected)
    {
        PlayersController.ScoreNameMatch(name, needle).Should().Be(expected);
    }
}
