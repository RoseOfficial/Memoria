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

public class PlayersControllerPatchTests
{
    private static PlayersController MakeController(AlphaScopeDbContext ctx, int? viewerUserId)
    {
        var c = new PlayersController(ctx, NullLogger<PlayersController>.Instance);
        var http = new DefaultHttpContext();
        http.Items["ViewerUserId"] = viewerUserId;
        c.ControllerContext = new ControllerContext { HttpContext = http };
        return c;
    }

    [Fact]
    public async Task Patch_OwnerCanSetHideAlts()
    {
        using var ctx = new AlphaScopeDbContext(DatabaseTestUtilities.CreateInMemoryDbOptions<AlphaScopeDbContext>());
        ctx.Users.Add(new ApplicationUser { Id = 5, ApiKey = "x", DiscordUserId = 1 });
        ctx.Players.Add(new Player { LocalContentId = 1, Name = "A", ClaimedByUserId = 5 });
        await ctx.SaveChangesAsync();

        var controller = MakeController(ctx, viewerUserId: 5);
        var result = await controller.Patch(1, new PlayerPrivacyPatchRequest(HideAlts: true, null, null));

        result.Should().BeOfType<NoContentResult>();
        (await ctx.Players.FindAsync(1L))!.HideAlts.Should().BeTrue();
    }

    [Fact]
    public async Task Patch_NonOwnerReturns404()
    {
        using var ctx = new AlphaScopeDbContext(DatabaseTestUtilities.CreateInMemoryDbOptions<AlphaScopeDbContext>());
        ctx.Users.Add(new ApplicationUser { Id = 5, ApiKey = "x", DiscordUserId = 1 });
        ctx.Players.Add(new Player { LocalContentId = 1, Name = "A", ClaimedByUserId = 5 });
        await ctx.SaveChangesAsync();

        var controller = MakeController(ctx, viewerUserId: 99);
        var result = await controller.Patch(1, new PlayerPrivacyPatchRequest(true, null, null));
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Patch_Anonymous_Returns401()
    {
        using var ctx = new AlphaScopeDbContext(DatabaseTestUtilities.CreateInMemoryDbOptions<AlphaScopeDbContext>());
        ctx.Players.Add(new Player { LocalContentId = 1, Name = "A", ClaimedByUserId = 5 });
        await ctx.SaveChangesAsync();

        var controller = MakeController(ctx, viewerUserId: null);
        var result = await controller.Patch(1, new PlayerPrivacyPatchRequest(true, null, null));
        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task Patch_OnlyNonNullFieldsApply()
    {
        using var ctx = new AlphaScopeDbContext(DatabaseTestUtilities.CreateInMemoryDbOptions<AlphaScopeDbContext>());
        ctx.Users.Add(new ApplicationUser { Id = 5, ApiKey = "x", DiscordUserId = 1 });
        ctx.Players.Add(new Player { LocalContentId = 1, Name = "A", ClaimedByUserId = 5, HideEncounters = true });
        await ctx.SaveChangesAsync();

        var controller = MakeController(ctx, viewerUserId: 5);
        await controller.Patch(1, new PlayerPrivacyPatchRequest(HideAlts: true, HideEncounters: null, HideEntirely: null));

        var p = await ctx.Players.FindAsync(1L);
        p!.HideAlts.Should().BeTrue();
        p.HideEncounters.Should().BeTrue(); // unchanged
    }
}
