using AlphaScopeServer.Controllers;
using AlphaScopeServer.Data;
using AlphaScopeServer.Models.Entities;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using TestUtilities;
using Xunit;

namespace AlphaScopeServer.Tests.Controllers;

public class PlayersControllerBySlugAdminTests
{
    [Fact]
    public async Task GetBySlug_HideEntirely_Returns200ToAdmin()
    {
        using var ctx = new AlphaScopeDbContext(DatabaseTestUtilities.CreateInMemoryDbOptions<AlphaScopeDbContext>());
        ctx.Players.Add(new Player
        {
            LocalContentId = 1, Name = "Tataru Taru", HomeWorldId = 91, HideEntirely = true,
        });
        await ctx.SaveChangesAsync();

        var c = new PlayersController(ctx, NullLogger<PlayersController>.Instance);
        var http = new DefaultHttpContext();
        http.Items["Tier"] = 1;
        http.Items["ViewerUserId"] = (int?)99;
        http.Items["IsAdmin"] = true;
        c.ControllerContext = new ControllerContext { HttpContext = http };

        var result = await c.GetBySlug("balmung", "tataru-taru");
        result.Should().BeOfType<OkObjectResult>();
    }
}
