using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AlphaScopeServer.Tests.Infrastructure;

/// <summary>
/// Verifies that the API key middleware, when wired into Program.cs, rejects unauthenticated
/// callers on protected routes and lets them through on the exempted routes.
/// </summary>
public class AuthMiddlewareIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public AuthMiddlewareIntegrationTests(WebApplicationFactory<Program> factory)
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
    public async Task AuthPaths_AreReachableWithoutApiKey()
    {
        using var client = _factory.CreateClient();
        // /users/create-test-user must be reachable so the plugin can self-register.
        var response = await client.PostAsJsonAsync("/v1/users/create-test-user", new { Name = "probe" });
        // 401 would mean the middleware blocked it (bug). Anything else (400, 404, 500, 200) means
        // the middleware let it through and the downstream controller handled it on its own terms.
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }
}
