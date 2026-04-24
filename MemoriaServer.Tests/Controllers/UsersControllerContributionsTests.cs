using MemoriaServer.Controllers;
using MemoriaServer.Data;
using MemoriaServer.Models.DTOs;
using MemoriaServer.Models.Entities;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using TestUtilities;
using Xunit;

namespace MemoriaServer.Tests.Controllers;

public class UsersControllerContributionsTests
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
    public async Task GetContributions_Anonymous_Returns401()
    {
        using var ctx = new MemoriaDbContext(DatabaseTestUtilities.CreateInMemoryDbOptions<MemoriaDbContext>());
        var result = await MakeController(ctx, null).GetContributions();
        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetContributions_ReturnsLifetimeAndRecent()
    {
        using var ctx = new MemoriaDbContext(DatabaseTestUtilities.CreateInMemoryDbOptions<MemoriaDbContext>());
        ctx.Users.Add(new ApplicationUser { Id = 5, ApiKey = "x", DiscordUserId = 1, TotalContributions = 1234 });
        await ctx.SaveChangesAsync();

        var result = await MakeController(ctx, 5).GetContributions();
        var dto = ((OkObjectResult)result).Value.Should().BeOfType<ContributionsResponse>().Subject;
        dto.Lifetime.Should().Be(1234);
        dto.Recent.Should().BeEmpty();
    }
}
