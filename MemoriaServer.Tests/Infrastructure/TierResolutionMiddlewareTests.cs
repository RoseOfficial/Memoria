using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using MemoriaServer.Data;
using MemoriaServer.Middleware;
using MemoriaServer.Models.Entities;
using MemoriaServer.Services.Admin;
using TestUtilities;

namespace MemoriaServer.Tests.Infrastructure;

public class TierResolutionMiddlewareTests : IDisposable
{
    private readonly MemoriaDbContext _db;
    private readonly RequestDelegate _next;
    private readonly TierResolutionMiddleware _middleware;

    public TierResolutionMiddlewareTests()
    {
        var options = DatabaseTestUtilities.CreateInMemoryDbOptions<MemoriaDbContext>();
        _db = new MemoriaDbContext(options);
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
    public async Task IsGuildMemberFresh_ReturnsTrue_WhenFreshAndMember()
    {
        var user = new ApplicationUser { Name = "U", ApiKey = "k", IsGuildMember = true, GuildMembershipCheckedAt = DateTime.UtcNow, PrimaryCharacterLocalContentId = 0 };
        var result = await TierResolutionMiddleware.IsGuildMemberFresh(user, _db);
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsGuildMemberFresh_ReturnsFalse_WhenFreshButNotMember()
    {
        var user = new ApplicationUser { Name = "U", ApiKey = "k", IsGuildMember = false, GuildMembershipCheckedAt = DateTime.UtcNow, PrimaryCharacterLocalContentId = 0 };
        var result = await TierResolutionMiddleware.IsGuildMemberFresh(user, _db);
        result.Should().BeFalse();
    }

    [Fact]
    public async Task Middleware_SetsIsAdminTrue_WhenUserInAllowlist()
    {
        using var ctx = new MemoriaDbContext(DatabaseTestUtilities.CreateInMemoryDbOptions<MemoriaDbContext>());
        var user = new ApplicationUser { Id = 1, ApiKey = "x", DiscordUserId = 12345 };
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        var options = Options.Create(new AdminOptions { DiscordUserIds = new() { 12345 } });
        var http = new DefaultHttpContext();
        http.Items["User"] = user;
        http.RequestServices = new ServiceCollection().AddSingleton(options).BuildServiceProvider();

        var middleware = new TierResolutionMiddleware(next: (_) => Task.CompletedTask);
        await middleware.InvokeAsync(http, ctx);

        http.Items["IsAdmin"].Should().Be(true);
    }

    [Fact]
    public async Task Middleware_SetsIsAdminFalse_WhenUserNotInAllowlist()
    {
        using var ctx = new MemoriaDbContext(DatabaseTestUtilities.CreateInMemoryDbOptions<MemoriaDbContext>());
        var user = new ApplicationUser { Id = 1, ApiKey = "x", DiscordUserId = 99 };
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        var options = Options.Create(new AdminOptions { DiscordUserIds = new() { 12345 } });
        var http = new DefaultHttpContext();
        http.Items["User"] = user;
        http.RequestServices = new ServiceCollection().AddSingleton(options).BuildServiceProvider();

        var middleware = new TierResolutionMiddleware((_) => Task.CompletedTask);
        await middleware.InvokeAsync(http, ctx);

        http.Items["IsAdmin"].Should().Be(false);
    }

    [Fact]
    public async Task Middleware_SetsIsAdminFalse_WhenAnonymous()
    {
        using var ctx = new MemoriaDbContext(DatabaseTestUtilities.CreateInMemoryDbOptions<MemoriaDbContext>());
        var options = Options.Create(new AdminOptions { DiscordUserIds = new() { 12345 } });
        var http = new DefaultHttpContext();
        http.RequestServices = new ServiceCollection().AddSingleton(options).BuildServiceProvider();

        var middleware = new TierResolutionMiddleware((_) => Task.CompletedTask);
        await middleware.InvokeAsync(http, ctx);

        http.Items["IsAdmin"].Should().Be(false);
    }
}
