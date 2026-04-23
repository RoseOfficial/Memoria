using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using AlphaScopeServer.Data;
using AlphaScopeServer.Middleware;
using AlphaScopeServer.Models.Entities;
using TestUtilities;

namespace AlphaScopeServer.Tests.Infrastructure;

public class TierResolutionMiddlewareTests : IDisposable
{
    private readonly AlphaScopeDbContext _db;
    private readonly RequestDelegate _next;
    private readonly TierResolutionMiddleware _middleware;

    public TierResolutionMiddlewareTests()
    {
        var options = DatabaseTestUtilities.CreateInMemoryDbOptions<AlphaScopeDbContext>();
        _db = new AlphaScopeDbContext(options);
        _db.Database.EnsureCreated();

        _next = Substitute.For<RequestDelegate>();
        _middleware = new TierResolutionMiddleware(_next);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task InvokeAsync_AnonymousRequest_SetsTier1AndNullViewer()
    {
        var ctx = new DefaultHttpContext();
        // No context.Items["User"] set -> anonymous

        await _middleware.InvokeAsync(ctx, _db);

        ctx.Items["Tier"].Should().Be(1);
        ctx.Items["ViewerUserId"].Should().BeNull();
        await _next.Received(1).Invoke(ctx);
    }

    [Fact]
    public async Task InvokeAsync_SignedInUser_SetsTier1InStubBehaviorAndViewerId()
    {
        var user = new ApplicationUser { Id = 5, Name = "U", ApiKey = "k", PrimaryCharacterLocalContentId = 0 };
        var ctx = new DefaultHttpContext();
        ctx.Items["User"] = user;

        await _middleware.InvokeAsync(ctx, _db);

        ctx.Items["Tier"].Should().Be(1);
        ctx.Items["ViewerUserId"].Should().Be(5);
    }

    [Fact]
    public async Task IsGuildMemberFresh_ReturnsFalse_WhenCheckedAtNull()
    {
        var user = new ApplicationUser { Name = "U", ApiKey = "k", IsGuildMember = true, GuildMembershipCheckedAt = null, PrimaryCharacterLocalContentId = 0 };
        var result = await TierResolutionMiddleware.IsGuildMemberFresh(user, _db);
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsGuildMemberFresh_ReturnsFalse_WhenStale()
    {
        var user = new ApplicationUser { Name = "U", ApiKey = "k", IsGuildMember = true, GuildMembershipCheckedAt = DateTime.UtcNow.AddHours(-25), PrimaryCharacterLocalContentId = 0 };
        var result = await TierResolutionMiddleware.IsGuildMemberFresh(user, _db);
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsGuildMemberFresh_ReturnsFalse_InStubEvenWhenFresh()
    {
        // In Plan 0a the helper returns false unconditionally. Plan 0c fleshes it out.
        var user = new ApplicationUser { Name = "U", ApiKey = "k", IsGuildMember = true, GuildMembershipCheckedAt = DateTime.UtcNow, PrimaryCharacterLocalContentId = 0 };
        var result = await TierResolutionMiddleware.IsGuildMemberFresh(user, _db);
        result.Should().BeFalse();
    }
}
