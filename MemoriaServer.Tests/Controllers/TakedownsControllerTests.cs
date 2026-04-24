using MemoriaServer.Controllers;
using MemoriaServer.Data;
using MemoriaServer.Models.DTOs;
using MemoriaServer.Models.Entities;
using MemoriaServer.Services.Takedowns;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using TestUtilities;
using Xunit;

namespace MemoriaServer.Tests.Controllers;

public class TakedownsControllerTests
{
    private static TakedownsController MakeController(MemoriaDbContext ctx, TakedownRateLimiter? limiter = null, string ip = "1.2.3.4")
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
        using var ctx = new MemoriaDbContext(DatabaseTestUtilities.CreateInMemoryDbOptions<MemoriaDbContext>());
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
        using var ctx = new MemoriaDbContext(DatabaseTestUtilities.CreateInMemoryDbOptions<MemoriaDbContext>());
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
        using var ctx = new MemoriaDbContext(DatabaseTestUtilities.CreateInMemoryDbOptions<MemoriaDbContext>());
        ctx.Players.Add(new Player { LocalContentId = 42, Name = "Tataru Taru", HomeWorldId = 91 });
        await ctx.SaveChangesAsync();

        await MakeController(ctx).Submit(new("balmung", "tataru-taru", "r", "a@b.com"));

        ctx.TakedownRequests.First().ResolvedPlayerLocalContentId.Should().Be(42);
    }

    [Fact]
    public async Task List_NonAdmin_Returns404()
    {
        using var ctx = new MemoriaDbContext(DatabaseTestUtilities.CreateInMemoryDbOptions<MemoriaDbContext>());
        var c = MakeController(ctx);
        c.HttpContext.Items["IsAdmin"] = false;
        var result = await c.List("pending");
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task List_Admin_ReturnsPendingOrdered()
    {
        using var ctx = new MemoriaDbContext(DatabaseTestUtilities.CreateInMemoryDbOptions<MemoriaDbContext>());
        ctx.TakedownRequests.AddRange(
            new TakedownRequest { WorldSlug = "a", NameSlug = "b", Reason = "r", ContactEmail = "e@e", SubmitterIpHash = "h",
                SubmittedAt = DateTime.UtcNow.AddHours(-1), Status = TakedownStatus.Pending },
            new TakedownRequest { WorldSlug = "a", NameSlug = "c", Reason = "r", ContactEmail = "e@e", SubmitterIpHash = "h",
                SubmittedAt = DateTime.UtcNow, Status = TakedownStatus.Pending });
        await ctx.SaveChangesAsync();

        var c = MakeController(ctx);
        c.HttpContext.Items["IsAdmin"] = true;
        c.HttpContext.Items["ViewerUserId"] = 1;
        var result = await c.List("pending");
        var list = ((OkObjectResult)result).Value.Should().BeAssignableTo<IEnumerable<TakedownListItem>>().Subject.ToList();
        list.Should().HaveCount(2);
        list[0].NameSlug.Should().Be("b"); // older first
    }

    [Fact]
    public async Task Act_Approve_FlipsHideEntirelyAndResolves()
    {
        using var ctx = new MemoriaDbContext(DatabaseTestUtilities.CreateInMemoryDbOptions<MemoriaDbContext>());
        ctx.Users.Add(new ApplicationUser { Id = 9, ApiKey = "x", DiscordUserId = 1 });
        ctx.Players.Add(new Player { LocalContentId = 50, Name = "Tataru Taru", HomeWorldId = 91 });
        var td = new TakedownRequest { WorldSlug = "balmung", NameSlug = "tataru-taru", Reason = "r", ContactEmail = "e@e",
            SubmitterIpHash = "h", ResolvedPlayerLocalContentId = 50, Status = TakedownStatus.Pending };
        ctx.TakedownRequests.Add(td);
        await ctx.SaveChangesAsync();

        var c = MakeController(ctx);
        c.HttpContext.Items["IsAdmin"] = true;
        c.HttpContext.Items["ViewerUserId"] = 9;
        var result = await c.Act(td.Id, new TakedownActionRequest("approve", null));

        result.Should().BeOfType<NoContentResult>();
        (await ctx.Players.FindAsync(50L))!.HideEntirely.Should().BeTrue();
        (await ctx.TakedownRequests.FindAsync(td.Id))!.Status.Should().Be(TakedownStatus.Resolved);
    }
}
