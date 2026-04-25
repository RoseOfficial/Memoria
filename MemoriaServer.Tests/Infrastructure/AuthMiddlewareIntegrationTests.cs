using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MemoriaServer.Tests.Infrastructure;

/// <summary>
/// Verifies that the API key middleware, when wired into Program.cs, rejects unauthenticated
/// callers on protected routes and lets them through on the exempted routes.
/// </summary>
public class AuthMiddlewareIntegrationTests : IClassFixture<TestAppFactory>
{
    private readonly TestAppFactory _factory;

    public AuthMiddlewareIntegrationTests(TestAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PostPlayers_WithoutApiKey_Returns401()
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/v1/players", new List<object>());
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetPlayers_WithoutApiKey_Returns401()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/v1/players");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task HealthEndpoint_IsReachableWithoutApiKey()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/health");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PublicReadPaths_AreReachableWithoutApiKey()
    {
        using var client = _factory.CreateClient();
        // Plan 0c public-read endpoints must be reachable to anon visitors.
        var response = await client.GetAsync("/v1/players/recent");
        // 401 would mean the middleware blocked it (bug). Anything else (200, 500) means
        // the middleware let it through and the downstream controller handled it.
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("/v1/auth/link/generate")]
    [InlineData("/v1/auth/link/redeem")]
    public async Task AuthLinkEndpoints_WithoutApiKey_AreRejectedByMiddleware(string path)
    {
        // Regression: link/generate and link/redeem are auth-required despite living under
        // /auth/. The middleware — not the controller — is the layer responsible for that
        // rejection. The plain-text "API key is required" body distinguishes a middleware
        // 401 from a controller-level Unauthorized() (which serializes ProblemDetails JSON).
        using var client = _factory.CreateClient();
        var response = await client.PostAsync(path, content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Be("API key is required");
    }
}
