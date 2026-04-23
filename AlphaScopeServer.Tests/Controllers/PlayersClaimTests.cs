using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using AlphaScopeServer.Controllers;
using AlphaScopeServer.Data;
using AlphaScopeServer.Models.Entities;
using TestUtilities;
using System.Net;

namespace AlphaScopeServer.Tests.Controllers;

/// <summary>
/// Tests for POST /v1/players/{localContentId}/claim/start.
/// Uses the direct-controller pattern (same as PlayersControllerTests) so there is no
/// middleware, network stack, or WebApplicationFactory DB-isolation puzzle.
/// HttpContext.Items["User"] is set manually to simulate the API-key middleware resolving
/// an authenticated user before the controller action runs.
/// </summary>
public class PlayersClaimTests : IDisposable
{
    private readonly AlphaScopeDbContext _context;
    private readonly ILogger<PlayersController> _mockLogger;
    private readonly PlayersController _controller;
    private readonly ApplicationUser _user;

    public PlayersClaimTests()
    {
        var options = DatabaseTestUtilities.CreateInMemoryDbOptions<AlphaScopeDbContext>();
        _context = new AlphaScopeDbContext(options);
        _context.Database.EnsureCreated();

        _mockLogger = LoggerTestUtilities.CreateMockLogger<PlayersController>();
        _controller = new PlayersController(_context, _mockLogger);

        _user = new ApplicationUser
        {
            Name = "TestUser",
            ApiKey = "TESTKEY123",
            PrimaryCharacterLocalContentId = 0
        };
        _context.Users.Add(_user);
        _context.SaveChanges();

        var httpContext = new DefaultHttpContext();
        httpContext.Items["User"] = _user;
        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
    }

    public void Dispose() => _context?.Dispose();

    [Fact]
    public async Task Start_ReturnsCodeAnd24hExpiry()
    {
        var player = new Player { LocalContentId = 1001, Name = "Alt" };
        _context.Players.Add(player);
        _context.SaveChanges();
        _context.PlayerLodestones.Add(new PlayerLodestone { PlayerLocalContentId = 1001, LodestoneId = 777 });
        _context.SaveChanges();

        var result = await _controller.StartClaim(1001);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var code = ok.Value!.GetType().GetProperty("Code")!.GetValue(ok.Value) as string;
        var expiresAt = (DateTime)ok.Value!.GetType().GetProperty("ExpiresAt")!.GetValue(ok.Value)!;
        var instructions = ok.Value!.GetType().GetProperty("Instructions")!.GetValue(ok.Value) as string;

        code.Should().StartWith("AS-");
        (expiresAt - DateTime.UtcNow).TotalHours.Should().BeApproximately(24, 0.1);
        instructions.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Start_Twice_UpsertsAttempt()
    {
        var player = new Player { LocalContentId = 1002, Name = "AltUpsert" };
        _context.Players.Add(player);
        _context.SaveChanges();
        _context.PlayerLodestones.Add(new PlayerLodestone { PlayerLocalContentId = 1002, LodestoneId = 888 });
        _context.SaveChanges();

        var r1 = await _controller.StartClaim(1002);
        var ok1 = r1.Should().BeOfType<OkObjectResult>().Subject;
        var code1 = ok1.Value!.GetType().GetProperty("Code")!.GetValue(ok1.Value) as string;

        var r2 = await _controller.StartClaim(1002);
        var ok2 = r2.Should().BeOfType<OkObjectResult>().Subject;
        var code2 = ok2.Value!.GetType().GetProperty("Code")!.GetValue(ok2.Value) as string;

        // Each call should issue a fresh code.
        code1.Should().StartWith("AS-");
        code2.Should().StartWith("AS-");
        code1.Should().NotBe(code2);

        // Exactly one row in the ClaimAttempts table (upserted, not duplicated).
        var count = await _context.ClaimAttempts
            .CountAsync(a => a.UserId == _user.Id && a.PlayerLocalContentId == 1002);
        count.Should().Be(1);
    }

    [Fact]
    public async Task Start_WithoutLodestoneId_Returns412()
    {
        var player = new Player { LocalContentId = 1003, Name = "NoLodestone" };
        _context.Players.Add(player);
        _context.SaveChanges();
        // No PlayerLodestone row added.

        var result = await _controller.StartClaim(1003);

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(412);
    }

    [Fact]
    public async Task Start_PlayerNotFound_Returns404()
    {
        var result = await _controller.StartClaim(9999);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Start_NoUserInContext_Returns401()
    {
        // Simulate a request that bypassed the API-key middleware (HttpContext.Items["User"] is absent).
        var controller = new PlayersController(_context, _mockLogger);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        var player = new Player { LocalContentId = 1004, Name = "Public" };
        _context.Players.Add(player);
        _context.SaveChanges();

        var result = await controller.StartClaim(1004);

        result.Should().BeOfType<UnauthorizedResult>();
    }
}
