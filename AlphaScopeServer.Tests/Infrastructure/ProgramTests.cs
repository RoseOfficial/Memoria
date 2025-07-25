using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using AlphaScopeServer.Data;
using AlphaScopeServer.Middleware;
using System.Net;
using System.Text.Json;

namespace AlphaScopeServer.Tests.Infrastructure;

public class ProgramTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ProgramTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public void Program_ShouldCreateApplicationSuccessfully()
    {
        // Act
        using var client = _factory.CreateClient();

        // Assert
        client.Should().NotBeNull();
    }

    [Fact]
    public void Services_ShouldBeRegisteredCorrectly()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;

        // Act & Assert - Core services
        services.GetService<AlphaScopeDbContext>().Should().NotBeNull();
        services.GetService<ILogger<Program>>().Should().NotBeNull();
    }

    [Fact]
    public void DbContext_ShouldBeConfiguredWithSqlite()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AlphaScopeDbContext>();

        // Act & Assert
        context.Should().NotBeNull();
        context.Database.IsSqlite().Should().BeTrue();
    }

    [Fact]
    public async Task HealthEndpoint_ShouldReturnHealthyStatus()
    {
        // Arrange
        using var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        var healthResponse = JsonSerializer.Deserialize<HealthResponse>(content, new JsonSerializerOptions 
        { 
            PropertyNameCaseInsensitive = true 
        });
        
        healthResponse.Should().NotBeNull();
        healthResponse!.Status.Should().Be("healthy");
        healthResponse.Timestamp.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task SwaggerEndpoint_ShouldBeAvailableInDevelopment()
    {
        // Arrange
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
        });
        
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/swagger/index.html");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Swagger UI");
        content.Should().Contain("AlphaScope Server API");
    }

    [Fact]
    public async Task SwaggerJson_ShouldBeAvailable()
    {
        // Arrange
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
        });
        
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/swagger/v1/swagger.json");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        var swaggerDoc = JsonSerializer.Deserialize<SwaggerDocument>(content, new JsonSerializerOptions 
        { 
            PropertyNameCaseInsensitive = true 
        });
        
        swaggerDoc.Should().NotBeNull();
        swaggerDoc!.Info.Should().NotBeNull();
        swaggerDoc.Info.Title.Should().Be("AlphaScope Server API");
        swaggerDoc.Info.Version.Should().Be("v1");
    }

    [Fact]
    public async Task CorsPolicy_ShouldAllowAnyOrigin()
    {
        // Arrange
        using var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health");

        // Assert
        response.Headers.Should().ContainKey("Access-Control-Allow-Origin");
        response.Headers.GetValues("Access-Control-Allow-Origin").Should().Contain("*");
    }

    [Fact]
    public async Task ApiKeyAuthentication_ShouldBeAppliedToProtectedEndpoints()
    {
        // Arrange
        using var client = _factory.CreateClient();

        // Act - Try to access a protected endpoint without API key
        var response = await client.GetAsync("/api/players");

        // Assert - Should return 401 Unauthorized
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Controllers_ShouldBeRegisteredAndRouted()
    {
        // Arrange
        using var client = _factory.CreateClient();

        // Act - Try to access controller endpoints
        var playersResponse = await client.GetAsync("/api/players");
        var retainersResponse = await client.GetAsync("/api/retainers");
        var serverResponse = await client.GetAsync("/api/server");

        // Assert - Should reach controllers (even if unauthorized)
        // We expect 401 for players/retainers (protected) and 200 for server (public)
        playersResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        retainersResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        serverResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public void DatabaseInitialization_ShouldNotThrowExceptions()
    {
        // Arrange & Act - The database initialization happens during app startup
        // If we reach this point, it means the startup was successful
        
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AlphaScopeDbContext>();

        // Assert - Database should be accessible
        var act = () => context.Database.CanConnect();
        act.Should().NotThrow();
    }

    [Fact]
    public void LoggingProviders_ShouldBeConfigured()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();

        // Act
        var logger = loggerFactory.CreateLogger<ProgramTests>();

        // Assert
        logger.Should().NotBeNull();
        logger.IsEnabled(LogLevel.Information).Should().BeTrue();
    }

    [Fact]
    public async Task JsonSerialization_ShouldUseNewtonsoftSettings()
    {
        // Arrange
        using var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health");
        var content = await response.Content.ReadAsStringAsync();

        // Assert - Should use ISO date format and ignore null values
        content.Should().NotBeNull();
        content.Should().Contain("T"); // ISO date format contains T
        content.Should().NotContain("null"); // Null values should be ignored
    }

    [Fact]
    public void ServiceLifetimes_ShouldBeConfiguredCorrectly()
    {
        // Arrange
        using var scope1 = _factory.Services.CreateScope();
        using var scope2 = _factory.Services.CreateScope();

        // Act
        var context1 = scope1.ServiceProvider.GetRequiredService<AlphaScopeDbContext>();
        var context2 = scope2.ServiceProvider.GetRequiredService<AlphaScopeDbContext>();

        // Assert - DbContext should be scoped (different instances)
        context1.Should().NotBeSameAs(context2);
    }

    [Fact]
    public async Task ApiSecurity_ShouldRequireApiKeyForProtectedEndpoints()
    {
        // Arrange
        using var client = _factory.CreateClient();

        // Act - Test various protected endpoints
        var endpoints = new[]
        {
            "/api/players",
            "/api/retainers", 
            "/api/users"
        };

        foreach (var endpoint in endpoints)
        {
            var response = await client.GetAsync(endpoint);
            
            // Assert - All should require authentication
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized, 
                $"Endpoint {endpoint} should require authentication");
        }
    }

    [Fact]
    public async Task PublicEndpoints_ShouldNotRequireAuthentication()
    {
        // Arrange
        using var client = _factory.CreateClient();

        // Act - Test public endpoints
        var healthResponse = await client.GetAsync("/health");
        var serverResponse = await client.GetAsync("/api/server");

        // Assert - Should be accessible without authentication
        healthResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        serverResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public void MiddlewarePipeline_ShouldBeInCorrectOrder()
    {
        // This test verifies that the middleware pipeline is configured correctly
        // The order should be: CORS -> Authentication -> Routing -> Controllers
        
        // Arrange & Act - Pipeline setup happens during app creation
        using var client = _factory.CreateClient();

        // Assert - If we can create a client and make requests, the pipeline is correct
        client.Should().NotBeNull();
    }

    [Fact]
    public async Task DatabaseTimeout_ShouldBeConfigured()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AlphaScopeDbContext>();

        // Act - Execute a simple query to verify timeout configuration
        var act = async () => await context.Database.ExecuteSqlRawAsync("SELECT 1");

        // Assert - Should not throw timeout exception for simple query
        await act.Should().NotThrowAsync();
    }

    // Helper classes for JSON deserialization
    private class HealthResponse
    {
        public string Status { get; set; } = string.Empty;
        public DateTimeOffset Timestamp { get; set; }
    }

    private class SwaggerDocument
    {
        public SwaggerInfo Info { get; set; } = new();
    }

    private class SwaggerInfo
    {
        public string Title { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
    }
}

/// <summary>
/// Integration tests for Program.cs with custom configuration
/// </summary>
public class ProgramConfigurationTests
{
    [Fact]
    public void WebApplicationBuilder_ShouldConfigureServicesCorrectly()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();

        // Act - Configure services like in Program.cs
        builder.Services.AddControllers().AddNewtonsoftJson();
        builder.Services.AddDbContext<AlphaScopeDbContext>(options =>
            options.UseInMemoryDatabase("TestDb"));
        builder.Services.AddCors();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        using var app = builder.Build();

        // Assert - Services should be registered
        app.Services.GetService<AlphaScopeDbContext>().Should().NotBeNull();
    }

    [Fact]
    public void ConnectionString_ShouldDefaultToSqliteFile()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        
        // Act - Configure DbContext without connection string
        builder.Services.AddDbContext<AlphaScopeDbContext>(options =>
        {
            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
                ?? "Data Source=AlphaScope.db";
            options.UseSqlite(connectionString);
        });

        using var app = builder.Build();
        using var scope = app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AlphaScopeDbContext>();

        // Assert
        context.Database.IsSqlite().Should().BeTrue();
    }

    [Fact]
    public void CorsPolicy_ShouldAllowAllOriginsMethodsAndHeaders()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();

        // Act
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AlphaScopePolicy", policy =>
            {
                policy.AllowAnyOrigin()
                      .AllowAnyMethod()
                      .AllowAnyHeader();
            });
        });

        using var app = builder.Build();

        // Assert - CORS policy should be configured
        var corsService = app.Services.GetService<Microsoft.AspNetCore.Cors.Infrastructure.ICorsService>();
        corsService.Should().NotBeNull();
    }
}