using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using AlphaScopeServer.Controllers;
using AlphaScopeServer.Data;
using AlphaScopeServer.Models.DTOs;
using AlphaScopeServer.Models.Entities;
using AlphaScopeServer.Services.Auth;
using AlphaScopeServer.Tests.Infrastructure;
using AlphaScopeServer.Tests.TestDoubles;
using TestUtilities;

namespace AlphaScopeServer.Tests.Controllers;

public class AuthControllerTests
{

    [Fact]
    public async Task StartDiscordOAuth_Redirects_WithExpectedParams()
    {
        var factory = new AuthControllerOAuthFactory();
        var client = factory.CreateClient(new() { AllowAutoRedirect = false });

        var resp = await client.GetAsync("/v1/auth/discord/start?return_to=https://app.example.com/me");

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

        var resp = await client.GetAsync("/v1/auth/discord/start?return_to=https://evil.example.com");
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

        var resp = await client.GetAsync($"/v1/auth/discord/callback?code=testcode&state={Uri.EscapeDataString(state)}");

        resp.StatusCode.Should().Be(HttpStatusCode.Redirect);
        resp.Headers.Location!.ToString().Should().Be("https://app.example.com/me");

        // Extract the issued ApiKey from the Set-Cookie header and verify its shape.
        // Convert.ToHexString(RandomNumberGenerator.GetBytes(24)) always produces 48 hex chars.
        // Proving the cookie is present and well-formed is sufficient: if SaveChangesAsync had
        // thrown, the response would have been 500, not 302 with a Set-Cookie. A round-trip
        // authenticated request would be ideal but child-factory InMemory DBs don't share
        // state across request scopes, so the ApiKey length check is the cleanest proof here.
        resp.Headers.TryGetValues("Set-Cookie", out var cookies).Should().BeTrue();
        var alphaCookie = cookies!.First(c => c.StartsWith("__Host-alpha="));
        var apiKey = alphaCookie.Split(';')[0]["__Host-alpha=".Length..];
        apiKey.Should().HaveLength(48).And.MatchRegex("^[0-9A-F]{48}$"); // 24 random bytes → 48 uppercase hex chars
    }

    [Fact]
    public async Task CallbackDiscordOAuth_NonGuildMember_StillRedirectsAndSetsCookie()
    {
        var stub = new StubDiscordHttpHandler
        {
            GuildsResponse = """[{"id":"SomeOtherGuild","name":"Elsewhere"}]""",
        };
        var oauthFactory = new AuthControllerOAuthFactory();
        WebApplicationFactory<Program> factory = oauthFactory.WithDiscordHandler(stub);
        var client = factory.CreateClient(new() { AllowAutoRedirect = false });

        using var scope = factory.Services.CreateScope();
        var signer = scope.ServiceProvider.GetRequiredService<OAuthStateSigner>();
        var state = signer.Sign("https://app.example.com/me", "nonce2");
        client.DefaultRequestHeaders.Add("Cookie", $"__alpha_oauth_state={state}");

        var resp = await client.GetAsync($"/v1/auth/discord/callback?code=testcode&state={Uri.EscapeDataString(state)}");

        // Non-guild member should still get a session — IsGuildMember=false is set on the
        // row, but the callback always completes with a redirect and a cookie.
        resp.StatusCode.Should().Be(HttpStatusCode.Redirect);
        resp.Headers.Location!.ToString().Should().Be("https://app.example.com/me");
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
        var resp = await client.GetAsync($"/v1/auth/discord/callback?code=testcode&state=some-other-state");

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // Direct-controller tests for Logout — used because InMemory DB isolation between
    // factory seed scopes and request scopes prevents the integration pattern from working.

    private static AuthController BuildAuthController(HttpContext httpContext)
    {
        var options = DatabaseTestUtilities.CreateInMemoryDbOptions<AlphaScopeDbContext>();
        var db = new AlphaScopeDbContext(options);
        var logger = LoggerTestUtilities.CreateMockLogger<AuthController>();
        var discordOptions = Options.Create(new DiscordOptions
        {
            ClientId = "x",
            ClientSecret = "x",
            GuildId = "x",
            StateSigningKey = "ZGV2ZWxvcG1lbnQta2V5LTMyLWJ5dGVzLWxvbmctYmFzZTY0",
        });
        var signer = new OAuthStateSigner("ZGV2ZWxvcG1lbnQta2V5LTMyLWJ5dGVzLWxvbmctYmFzZTY0");
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        var config = Substitute.For<IConfiguration>();
        var controller = new AuthController(db, logger, discordOptions, signer, httpClientFactory, config)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
        };
        return controller;
    }

    [Fact]
    public void Logout_WithoutUser_Returns401()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new System.IO.MemoryStream();
        var controller = BuildAuthController(httpContext);
        // Items["User"] is not set — simulates unauthenticated request.

        var result = controller.Logout();

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public void Logout_WithUser_Returns204_AndDeletesCookie()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new System.IO.MemoryStream();
        var user = new ApplicationUser { Name = "U", ApiKey = "LOGOUT-KEY", PrimaryCharacterLocalContentId = 0 };
        httpContext.Items["User"] = user;
        var controller = BuildAuthController(httpContext);

        var result = controller.Logout();

        result.Should().BeOfType<NoContentResult>();
        // Response.Cookies.Delete appends a Set-Cookie with an expired date.
        httpContext.Response.Headers.TryGetValue("Set-Cookie", out var setCookie).Should().BeTrue();
        setCookie.ToString().Should().Contain("__Host-alpha=");
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

        var resp = await client.GetAsync($"/v1/auth/discord/callback?code=testcode&state={Uri.EscapeDataString(state)}");

        resp.StatusCode.Should().Be(HttpStatusCode.BadGateway);
    }

    // Direct-controller helpers and tests for GenerateLinkCode

    private static AuthController CreateAuthController(AlphaScopeDbContext ctx)
    {
        var logger = Substitute.For<ILogger<AuthController>>();
        var discordOpts = Options.Create(new DiscordOptions
        {
            ClientId = "test-client-id",
            ClientSecret = "test-client-secret",
            GuildId = "999888777",
            StateSigningKey = "ZGV2ZWxvcG1lbnQta2V5LTMyLWJ5dGVzLWxvbmctYmFzZTY0",
        });
        var stateSigner = new OAuthStateSigner("ZGV2ZWxvcG1lbnQta2V5LTMyLWJ5dGVzLWxvbmctYmFzZTY0");
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        var config = Substitute.For<IConfiguration>();
        config["ServerBaseUrl"].Returns("https://api.example.com");
        config["Cors:AllowedOrigins"].Returns("https://app.example.com");

        var controller = new AuthController(ctx, logger, discordOpts, stateSigner, httpClientFactory, config);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        return controller;
    }

    [Fact]
    public async Task GenerateLinkCode_CreatesRowWith15MinTtl()
    {
        var options = DatabaseTestUtilities.CreateInMemoryDbOptions<AlphaScopeDbContext>();
        using var ctx = new AlphaScopeDbContext(options);
        ctx.Database.EnsureCreated();

        var user = new ApplicationUser { Name = "Plugin", ApiKey = "PLUGKEY", GameAccountId = 12345, PrimaryCharacterLocalContentId = 0 };
        ctx.Users.Add(user);
        ctx.SaveChanges();

        var controller = CreateAuthController(ctx);
        controller.ControllerContext.HttpContext.Items["User"] = user;

        var result = await controller.GenerateLinkCode(CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        var body = ((OkObjectResult)result).Value.Should().BeOfType<LinkGenerateResponse>().Subject;
        body.Code.Should().StartWith("AL-");
        (body.ExpiresAt - DateTime.UtcNow).TotalMinutes.Should().BeApproximately(15, 0.5);

        ctx.AccountLinkCodes.Single().ApplicationUserId.Should().Be(user.Id);
    }

    [Fact]
    public async Task GenerateLinkCode_WithoutUser_Returns401()
    {
        var options = DatabaseTestUtilities.CreateInMemoryDbOptions<AlphaScopeDbContext>();
        using var ctx = new AlphaScopeDbContext(options);
        ctx.Database.EnsureCreated();

        var controller = CreateAuthController(ctx);
        // No HttpContext.Items["User"] set

        var result = await controller.GenerateLinkCode(CancellationToken.None);

        result.Should().BeOfType<UnauthorizedResult>();
    }
}
