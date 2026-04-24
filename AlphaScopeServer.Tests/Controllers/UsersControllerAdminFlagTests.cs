using AlphaScopeServer.Controllers;
using AlphaScopeServer.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using TestUtilities;
using Xunit;

namespace AlphaScopeServer.Tests.Controllers;

public class UsersControllerAdminFlagTests
{
    private static UsersController MakeController(AlphaScopeDbContext ctx, bool isAdmin, bool authenticated)
    {
        var c = new UsersController(ctx, NullLogger<UsersController>.Instance);
        var http = new DefaultHttpContext();
        http.Items["IsAdmin"] = isAdmin;
        http.Items["ViewerUserId"] = authenticated ? (int?)1 : null;
        c.ControllerContext = new ControllerContext { HttpContext = http };
        return c;
    }

    [Fact]
    public async Task GetAdminFlag_Anon_Returns401()
    {
        using var ctx = new AlphaScopeDbContext(DatabaseTestUtilities.CreateInMemoryDbOptions<AlphaScopeDbContext>());
        var r = await MakeController(ctx, isAdmin: false, authenticated: false).GetAdminFlag();
        r.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetAdminFlag_AuthNonAdmin_ReturnsFalse()
    {
        using var ctx = new AlphaScopeDbContext(DatabaseTestUtilities.CreateInMemoryDbOptions<AlphaScopeDbContext>());
        var r = await MakeController(ctx, isAdmin: false, authenticated: true).GetAdminFlag();
        var dto = ((OkObjectResult)r).Value;
        dto!.GetType().GetProperty("IsAdmin")!.GetValue(dto).Should().Be(false);
    }

    [Fact]
    public async Task GetAdminFlag_Admin_ReturnsTrue()
    {
        using var ctx = new AlphaScopeDbContext(DatabaseTestUtilities.CreateInMemoryDbOptions<AlphaScopeDbContext>());
        var r = await MakeController(ctx, isAdmin: true, authenticated: true).GetAdminFlag();
        var dto = ((OkObjectResult)r).Value;
        dto!.GetType().GetProperty("IsAdmin")!.GetValue(dto).Should().Be(true);
    }
}
