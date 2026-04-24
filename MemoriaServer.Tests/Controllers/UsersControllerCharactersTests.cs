using MemoriaServer.Controllers;
using MemoriaServer.Data;
using MemoriaServer.Models.Entities;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections;
using TestUtilities;
using Xunit;

namespace MemoriaServer.Tests.Controllers;

public class UsersControllerCharactersTests
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
    public async Task GetCharacters_Anon_Returns401()
    {
        using var ctx = new MemoriaDbContext(DatabaseTestUtilities.CreateInMemoryDbOptions<MemoriaDbContext>());
        var result = await MakeController(ctx, null).GetCharacters();
        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetCharacters_ReturnsOnlyClaimedByViewer()
    {
        using var ctx = new MemoriaDbContext(DatabaseTestUtilities.CreateInMemoryDbOptions<MemoriaDbContext>());
        ctx.Users.Add(new ApplicationUser { Id = 5, ApiKey = "x", DiscordUserId = 1 });
        ctx.Players.AddRange(
            new Player { LocalContentId = 1, Name = "Mine", HomeWorldId = 91, ClaimedByUserId = 5 },
            new Player { LocalContentId = 2, Name = "Yours", HomeWorldId = 91, ClaimedByUserId = 99 },
            new Player { LocalContentId = 3, Name = "Unclaimed", HomeWorldId = 91 });
        await ctx.SaveChangesAsync();

        var result = await MakeController(ctx, 5).GetCharacters();
        var list = ((OkObjectResult)result).Value.Should().BeAssignableTo<IEnumerable>().Subject;
        list.Cast<object>().Count().Should().Be(1);
    }
}
