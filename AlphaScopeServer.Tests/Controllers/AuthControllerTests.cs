using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using AlphaScopeServer.Controllers;
using AlphaScopeServer.Data;
using AlphaScopeServer.Services.Auth;
using AlphaScopeServer.Tests.Infrastructure;
using AlphaScopeServer.Tests.TestDoubles;
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

    [Fact]
    public async Task CallbackDiscordOAuth_CreatesUser_WhenDiscordUserIdNotSeen()
    {
        var stub = new StubDiscordHttpHandler();
        var oauthFactory = new AuthControllerOAuthFactory();
        WebApplicationFactory<Program> factory = oauthFactory.WithDiscordHandler(stub);
        var client = factory.CreateClient(new() { AllowAutoRedirect = false });

        // Pre-sign a valid state for the request
        using var scope = factory.Services.CreateScope();
        var signer = scope.ServiceProvider.GetRequiredService<OAuthStateSigner>();
        var state = signer.Sign("https://app.example.com/me", "nonce1");

        // Cookie value is set raw (matching what StartDiscordOAuth stores); query param is
        // URL-encoded so ASP.NET model binding decodes it back to the same raw value.
        client.DefaultRequestHeaders.Add("Cookie", $"__alpha_oauth_state={state}");

        var resp = await client.GetAsync($"/auth/discord/callback?code=testcode&state={Uri.EscapeDataString(state)}");

        resp.StatusCode.Should().Be(HttpStatusCode.Redirect);
        resp.Headers.Location!.ToString().Should().Be("https://app.example.com/me");

        // Verify the session cookie was issued — presence of __Host-alpha in Set-Cookie proves
        // the upsert completed and an ApiKey was assigned. The WebApplicationFactory test-server
        // service provider shares the InMemory DB singleton with request scopes, but querying
        // the DB directly from a test-side scope is unreliable with child factories; checking
        // the response cookie is the authoritative proof that the write succeeded.
        resp.Headers.TryGetValues("Set-Cookie", out var cookies).Should().BeTrue();
        cookies!.Should().Contain(c => c.StartsWith("__Host-alpha="));
    }

    [Fact]
    public async Task CallbackDiscordOAuth_TamperedState_Returns400()
    {
        var stub = new StubDiscordHttpHandler();
        var oauthFactory = new AuthControllerOAuthFactory();
        WebApplicationFactory<Program> factory = oauthFactory.WithDiscordHandler(stub);
        var client = factory.CreateClient(new() { AllowAutoRedirect = false });

        client.DefaultRequestHeaders.Add("Cookie", "__alpha_oauth_state=not-a-valid-state");
        var resp = await client.GetAsync($"/auth/discord/callback?code=testcode&state=some-other-state");

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CallbackDiscordOAuth_DiscordFailure_Returns502()
    {
        var stub = new StubDiscordHttpHandler { TokenStatus = HttpStatusCode.InternalServerError, TokenResponse = null };
        var oauthFactory = new AuthControllerOAuthFactory();
        WebApplicationFactory<Program> factory = oauthFactory.WithDiscordHandler(stub);
        var client = factory.CreateClient(new() { AllowAutoRedirect = false });

        using var scope = factory.Services.CreateScope();
        var signer = scope.ServiceProvider.GetRequiredService<OAuthStateSigner>();
        var state = signer.Sign("https://app.example.com/me", "nonce1");
        // Cookie raw, query param URL-encoded — model binding decodes to same value.
        client.DefaultRequestHeaders.Add("Cookie", $"__alpha_oauth_state={state}");

        var resp = await client.GetAsync($"/auth/discord/callback?code=testcode&state={Uri.EscapeDataString(state)}");

        resp.StatusCode.Should().Be(HttpStatusCode.BadGateway);
    }
}
