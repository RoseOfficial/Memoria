using AlphaScopeServer.Controllers;
using AlphaScopeServer.Data;
using AlphaScopeServer.Models.DTOs;
using AlphaScopeServer.Models.Entities;
using AlphaScopeServer.Services.Takedowns;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using TestUtilities;
using Xunit;

namespace AlphaScopeServer.Tests.Controllers;

public class TakedownsControllerTests
{
    private static TakedownsController MakeController(AlphaScopeDbContext ctx, TakedownRateLimiter? limiter = null, string ip = "1.2.3.4")
    {
        var c = new TakedownsController(ctx, limiter ?? new TakedownRateLimiter(), NullLogger<TakedownsController>.Instance);
        var http = new DefaultHttpContext();
        http.Connection.RemoteIpAddress = IPAddress.Parse(ip);
        c.ControllerContext = new ControllerContext { HttpContext = http };
        return c;
    }

    [Fact]
    public async Task Submit_Valid_Returns202AndPersists()
    {
        using var ctx = new AlphaScopeDbContext(DatabaseTestUtilities.CreateInMemoryDbOptions<AlphaScopeDbContext>());
        var result = await MakeController(ctx).Submit(new TakedownSubmitRequest(
            "balmung", "tataru-taru", "Please remove", "a@b.com"));

        result.Should().BeOfType<AcceptedResult>();
        ctx.TakedownRequests.Should().HaveCount(1);
        var row = ctx.TakedownRequests.First();
        row.Status.Should().Be(TakedownStatus.Pending);
        row.Reason.Should().Be("Please remove");
    }

    [Fact]
    public async Task Submit_OverRateLimit_Returns429()
    {
        using var ctx = new AlphaScopeDbContext(DatabaseTestUtilities.CreateInMemoryDbOptions<AlphaScopeDbContext>());
        var limiter = new TakedownRateLimiter();
        var c = MakeController(ctx, limiter);
        await c.Submit(new("a", "b", "r", "a@b.com"));
        await c.Submit(new("a", "b", "r", "a@b.com"));
        await c.Submit(new("a", "b", "r", "a@b.com"));
        var result = await c.Submit(new("a", "b", "r", "a@b.com"));
        result.Should().BeOfType<StatusCodeResult>().Which.StatusCode.Should().Be(429);
    }

    [Fact]
    public async Task Submit_ResolvesPlayerId_WhenPlayerExists()
    {
        using var ctx = new AlphaScopeDbContext(DatabaseTestUtilities.CreateInMemoryDbOptions<AlphaScopeDbContext>());
        ctx.Players.Add(new Player { LocalContentId = 42, Name = "Tataru Taru", HomeWorldId = 91 });
        await ctx.SaveChangesAsync();

        await MakeController(ctx).Submit(new("balmung", "tataru-taru", "r", "a@b.com"));

        ctx.TakedownRequests.First().ResolvedPlayerLocalContentId.Should().Be(42);
    }
}
