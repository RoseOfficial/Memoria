using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using AlphaScopeServer.Data;
using AlphaScopeServer.Middleware;
using AlphaScopeServer.Models.Entities;
using System.Security.Claims;
using System.Text;
using TestUtilities;

namespace AlphaScopeServer.Tests.Infrastructure;

public class ApiKeyAuthenticationMiddlewareTests : IDisposable
{
    private readonly AlphaScopeDbContext _dbContext;
    private readonly ILogger<ApiKeyAuthenticationMiddleware> _mockLogger;
    private readonly RequestDelegate _mockNext;
    private readonly ApiKeyAuthenticationMiddleware _middleware;

    public ApiKeyAuthenticationMiddlewareTests()
    {
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<AlphaScopeDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new AlphaScopeDbContext(options);

        _mockLogger = LoggerTestUtilities.CreateMockLogger<ApiKeyAuthenticationMiddleware>();
        _mockNext = Substitute.For<RequestDelegate>();
        _middleware = new ApiKeyAuthenticationMiddleware(_mockNext, _mockLogger);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Theory]
    [InlineData("/server")]
    [InlineData("/users/login")]
    [InlineData("/users/create-test-user")]
    [InlineData("/auth/callback")]
    [InlineData("/waitforlogin")]
    [InlineData("/swagger")]
    [InlineData("/health")]
    public async Task InvokeAsync_ShouldSkipAuthenticationForExemptPaths(string path)
    {
        // Arrange
        var context = CreateHttpContext();
        context.Request.Path = path;
        context.Request.Method = "GET";

        // Act
        await _middleware.InvokeAsync(context, _dbContext);

        // Assert
        await _mockNext.Received(1).Invoke(context);
        context.Response.StatusCode.Should().Be(200); // Default status
    }

    [Fact]
    public async Task InvokeAsync_ShouldReturn401WhenApiKeyHeaderMissing()
    {
        // Arrange
        var context = CreateHttpContext();
        context.Request.Path = "/api/players";

        // Act
        await _middleware.InvokeAsync(context, _dbContext);

        // Assert
        await _mockNext.DidNotReceive().Invoke(Arg.Any<HttpContext>());
        context.Response.StatusCode.Should().Be(401);
        
        var responseBody = await GetResponseBody(context);
        responseBody.Should().Be("API key is required");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task InvokeAsync_ShouldReturn401WhenApiKeyEmpty(string apiKey)
    {
        // Arrange
        var context = CreateHttpContext();
        context.Request.Path = "/api/players";
        context.Request.Headers["api-key"] = apiKey;

        // Act
        await _middleware.InvokeAsync(context, _dbContext);

        // Assert
        await _mockNext.DidNotReceive().Invoke(Arg.Any<HttpContext>());
        context.Response.StatusCode.Should().Be(401);
        
        var responseBody = await GetResponseBody(context);
        responseBody.Should().Be("API key is required");
    }

    [Theory]
    [InlineData("invalid-key")]
    [InlineData("key-without-accountid")]
    [InlineData("key-abc")]
    [InlineData("key-")]
    [InlineData("-123")]
    [InlineData("multiple-dash-key-123")]
    public async Task InvokeAsync_ShouldReturn401WhenApiKeyFormatInvalid(string apiKey)
    {
        // Arrange
        var context = CreateHttpContext();
        context.Request.Path = "/api/players";
        context.Request.Headers["api-key"] = apiKey;

        // Act
        await _middleware.InvokeAsync(context, _dbContext);

        // Assert
        await _mockNext.DidNotReceive().Invoke(Arg.Any<HttpContext>());
        context.Response.StatusCode.Should().Be(401);
        
        var responseBody = await GetResponseBody(context);
        responseBody.Should().Be("Invalid API key format");
    }

    [Fact]
    public async Task InvokeAsync_ShouldReturn401WhenUserNotFound()
    {
        // Arrange
        var context = CreateHttpContext();
        context.Request.Path = "/api/players";
        context.Request.Headers["api-key"] = "nonexistent-key-12345";

        // Act
        await _middleware.InvokeAsync(context, _dbContext);

        // Assert
        await _mockNext.DidNotReceive().Invoke(Arg.Any<HttpContext>());
        context.Response.StatusCode.Should().Be(401);
        
        var responseBody = await GetResponseBody(context);
        responseBody.Should().Be("Invalid API key");
    }

    [Fact]
    public async Task InvokeAsync_ShouldAuthenticateValidUser()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = 1,
            Name = "Test User",
            ApiKey = "test-key-12345",
            GameAccountId = 12345,
            PrimaryCharacterLocalContentId = 999999999,
            AppRoleId = 2,
            LastLoginAt = DateTime.UtcNow.AddDays(-1)
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var context = CreateHttpContext();
        context.Request.Path = "/api/players";
        context.Request.Headers["api-key"] = "test-key-12345";

        // Act
        await _middleware.InvokeAsync(context, _dbContext);

        // Assert
        await _mockNext.Received(1).Invoke(context);
        context.User.Should().NotBeNull();
        context.User.Identity!.IsAuthenticated.Should().BeTrue();
        context.User.Identity.AuthenticationType.Should().Be("ApiKey");

        // Check claims
        var claims = context.User.Claims.ToList();
        claims.Should().Contain(c => c.Type == ClaimTypes.NameIdentifier && c.Value == "1");
        claims.Should().Contain(c => c.Type == ClaimTypes.Name && c.Value == "Test User");
        claims.Should().Contain(c => c.Type == "GameAccountId" && c.Value == "12345");
        claims.Should().Contain(c => c.Type == "LocalContentId" && c.Value == "999999999");
        claims.Should().Contain(c => c.Type == "AppRoleId" && c.Value == "2");

        // Check context items
        context.Items["User"].Should().Be(user);
        context.Items["UserId"].Should().Be(1);
        context.Items["GameAccountId"].Should().Be(12345);

        // Check last login time was updated
        var updatedUser = await _dbContext.Users.FindAsync(1);
        updatedUser!.LastLoginAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task InvokeAsync_ShouldMatchUserByKeyPrefixAndAccountId()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = 1,
            Name = "Test User",
            ApiKey = "test-key-with-suffix-12345", // API key contains the user key as prefix
            GameAccountId = 12345,
            PrimaryCharacterLocalContentId = 999999999,
            AppRoleId = 1
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var context = CreateHttpContext();
        context.Request.Path = "/api/players";
        context.Request.Headers["api-key"] = "test-key-12345"; // Only the prefix part

        // Act
        await _middleware.InvokeAsync(context, _dbContext);

        // Assert
        await _mockNext.Received(1).Invoke(context);
        context.User.Identity!.IsAuthenticated.Should().BeTrue();
        context.Items["UserId"].Should().Be(1);
    }

    [Fact]
    public async Task InvokeAsync_ShouldNotMatchWrongAccountId()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = 1,
            Name = "Test User",
            ApiKey = "test-key-12345",
            GameAccountId = 12345, // Different account ID
            PrimaryCharacterLocalContentId = 999999999,
            AppRoleId = 1
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var context = CreateHttpContext();
        context.Request.Path = "/api/players";
        context.Request.Headers["api-key"] = "test-key-67890"; // Wrong account ID

        // Act
        await _middleware.InvokeAsync(context, _dbContext);

        // Assert
        await _mockNext.DidNotReceive().Invoke(Arg.Any<HttpContext>());
        context.Response.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task InvokeAsync_ShouldReturn500OnDatabaseError()
    {
        // Arrange
        var context = CreateHttpContext();
        context.Request.Path = "/api/players";
        context.Request.Headers["api-key"] = "test-key-12345";

        // Dispose the context to cause a database error
        _dbContext.Dispose();

        // Act
        await _middleware.InvokeAsync(context, _dbContext);

        // Assert
        await _mockNext.DidNotReceive().Invoke(Arg.Any<HttpContext>());
        context.Response.StatusCode.Should().Be(500);
        
        var responseBody = await GetResponseBody(context);
        responseBody.Should().Be("Authentication error");
    }

    [Fact]
    public void UseApiKeyAuthentication_ShouldRegisterMiddleware()
    {
        // Arrange
        var mockApplicationBuilder = Substitute.For<IApplicationBuilder>();
        mockApplicationBuilder.UseMiddleware<ApiKeyAuthenticationMiddleware>()
            .Returns(mockApplicationBuilder);

        // Act
        var result = mockApplicationBuilder.UseApiKeyAuthentication();

        // Assert
        result.Should().Be(mockApplicationBuilder);
        mockApplicationBuilder.Received(1).UseMiddleware<ApiKeyAuthenticationMiddleware>();
    }

    [Theory]
    [InlineData("test-key-123", 123)]
    [InlineData("abc123-456", 456)]
    [InlineData("complex_key!@#-789", 789)]
    public async Task InvokeAsync_ShouldParseApiKeyFormatCorrectly(string apiKey, int expectedAccountId)
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = 1,
            Name = "Test User",
            ApiKey = apiKey,
            GameAccountId = expectedAccountId,
            PrimaryCharacterLocalContentId = 999999999,
            AppRoleId = 1
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var context = CreateHttpContext();
        context.Request.Path = "/api/players";
        context.Request.Headers["api-key"] = apiKey;

        // Act
        await _middleware.InvokeAsync(context, _dbContext);

        // Assert
        await _mockNext.Received(1).Invoke(context);
        context.User.Identity!.IsAuthenticated.Should().BeTrue();
        
        var gameAccountIdClaim = context.User.FindFirst("GameAccountId");
        gameAccountIdClaim!.Value.Should().Be(expectedAccountId.ToString());
    }

    private static HttpContext CreateHttpContext()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<string> GetResponseBody(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        return await reader.ReadToEndAsync();
    }
}