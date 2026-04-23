using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using AlphaScopeServer.Controllers;
using AlphaScopeServer.Data;
using AlphaScopeServer.Tests.Infrastructure;
using TestUtilities;

namespace AlphaScopeServer.Tests.Controllers;

public class AuthControllerTests : IDisposable
{
    private readonly AlphaScopeDbContext _context;

    public AuthControllerTests()
    {
        var options = DatabaseTestUtilities.CreateInMemoryDbOptions<AlphaScopeDbContext>();
        _context = new AlphaScopeDbContext(options);
        _context.Database.EnsureCreated();
    }

    public void Dispose() => _context?.Dispose();

    [Fact]
    public async Task StartDiscordOAuth_Redirects_WithExpectedParams()
    {
        var factory = new AuthControllerOAuthFactory();
        var client = factory.CreateClient(new() { AllowAutoRedirect = false });

        var resp = await client.GetAsync("/auth/discord/start?return_to=https://app.example.com/me");

        resp.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var location = resp.Headers.Location!.ToString();
        location.Should().StartWith("https://discord.com/api/oauth2/authorize");
        location.Should().Contain("client_id=test-client-id");
        // Discord expects scope "identify guilds", may be URL-encoded as + or %20
        (location.Contains("scope=identify+guilds") || location.Contains("scope=identify%20guilds")).Should().BeTrue();
        location.Should().Contain("response_type=code");
        location.Should().Contain("state=");
        resp.Headers.TryGetValues("Set-Cookie", out var setCookies).Should().BeTrue();
        setCookies!.Should().Contain(c => c.StartsWith("__alpha_oauth_state="));
    }

    [Fact]
    public async Task StartDiscordOAuth_InvalidReturnTo_Returns400()
    {
        var factory = new AuthControllerOAuthFactory();
        var client = factory.CreateClient(new() { AllowAutoRedirect = false });

        var resp = await client.GetAsync("/auth/discord/start?return_to=https://evil.example.com");
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
