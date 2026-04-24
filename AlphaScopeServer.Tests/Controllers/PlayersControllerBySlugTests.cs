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

public class PlayersControllerBySlugTests
{
    private static PlayersController MakeController(AlphaScopeDbContext ctx, int? viewerUserId = null)
    {
        var c = new PlayersController(ctx, NullLogger<PlayersController>.Instance);
        c.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        c.HttpContext.Items["Tier"] = 1;
        c.HttpContext.Items["ViewerUserId"] = viewerUserId;
        return c;
    }

    [Fact]
    public async Task GetBySlug_CurrentMatch_Returns200WithHeader()
    {
        using var ctx = new AlphaScopeDbContext(DatabaseTestUtilities.CreateInMemoryDbOptions<AlphaScopeDbContext>());
        ctx.Players.Add(new Player
        {
            LocalContentId = 1,
            Name = "Tataru Taru",
            HomeWorldId = 91,  // Balmung
            CurrentWorldId = 91,
        });
        await ctx.SaveChangesAsync();

        var controller = MakeController(ctx);
        var result = await controller.GetBySlug("balmung", "tataru-taru");

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = ok.Value.Should().BeOfType<PlayerProfileResponse>().Subject;
        dto.Header.Name.Should().Be("Tataru Taru");
        dto.Header.WorldSlug.Should().Be("balmung");
        dto.Header.WorldName.Should().Be("Balmung");
    }

    [Fact]
    public async Task GetBySlug_UnknownSlug_Returns404()
    {
        using var ctx = new AlphaScopeDbContext(DatabaseTestUtilities.CreateInMemoryDbOptions<AlphaScopeDbContext>());
        var controller = MakeController(ctx);
        var result = await controller.GetBySlug("balmung", "nobody");
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetBySlug_UnknownWorld_Returns404()
    {
        using var ctx = new AlphaScopeDbContext(DatabaseTestUtilities.CreateInMemoryDbOptions<AlphaScopeDbContext>());
        var controller = MakeController(ctx);
        var result = await controller.GetBySlug("notaworld", "tataru-taru");
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetBySlug_Tier1_OmitsTier2Sections()
    {
        using var ctx = new AlphaScopeDbContext(DatabaseTestUtilities.CreateInMemoryDbOptions<AlphaScopeDbContext>());
        ctx.Players.Add(new Player
        {
            LocalContentId = 1,
            Name = "Tataru Taru",
            HomeWorldId = 91,
            AccountId = 500,
        });
        await ctx.SaveChangesAsync();

        var controller = MakeController(ctx);
        var result = await controller.GetBySlug("balmung", "tataru-taru");
        var dto = ((OkObjectResult)result).Value.Should().BeOfType<PlayerProfileResponse>().Subject;

        dto.Locations.Should().BeNull();
        dto.NameHistory.Should().BeNull();
        dto.WorldHistory.Should().BeNull();
        dto.Alts.Should().BeNull();
    }
}
