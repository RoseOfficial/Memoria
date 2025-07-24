using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using NSubstitute;
using AlphaScopeServer.Controllers;
using AlphaScopeServer.Data;
using AlphaScopeServer.Models.DTOs;
using AlphaScopeServer.Models.Entities;
using System.Text;
using System.Text.Json;
using TestUtilities;

namespace AlphaScopeServer.Tests.Controllers;

public class AuthControllerTests : IDisposable
{
    private readonly AlphaScopeDbContext _context;
    private readonly ILogger<AuthController> _mockLogger;
    private readonly AuthController _controller;

    public AuthControllerTests()
    {
        var options = DatabaseTestUtilities.CreateInMemoryDbOptions<AlphaScopeDbContext>();
        _context = new AlphaScopeDbContext(options);
        _context.Database.EnsureCreated();

        _mockLogger = LoggerTestUtilities.CreateMockLogger<AuthController>();
        _controller = new AuthController(_context, _mockLogger);

        // Setup HttpContext
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        _controller.ControllerContext.HttpContext.Request.Scheme = "https";
        _controller.ControllerContext.HttpContext.Request.Host = new HostString("localhost:5001");
    }

    public void Dispose()
    {
        _context?.Dispose();
    }

    [Fact]
    public void DiscordAuth_ShouldReturnBadRequest_WhenDataParameterMissing()
    {
        // Arrange
        // No data parameter provided

        // Act
        var result = _controller.DiscordAuth();

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequest = result as BadRequestObjectResult;
        badRequest!.Value.Should().Be("The data field is required");
    }

    [Fact]
    public void DiscordAuth_ShouldReturnBadRequest_WhenDataParameterEmpty()
    {
        // Arrange
        _controller.ControllerContext.HttpContext.Request.QueryString = new QueryString("?data=");

        // Act
        var result = _controller.DiscordAuth();

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequest = result as BadRequestObjectResult;
        // The controller returns "Invalid request" when an empty string is provided because
        // Convert.FromBase64String("") throws an exception that's caught
        badRequest!.Value.Should().Be("Invalid request");
    }

    [Fact]
    public void DiscordAuth_ShouldReturnBadRequest_WhenDataIsInvalidBase64()
    {
        // Arrange
        _controller.ControllerContext.HttpContext.Request.QueryString = new QueryString("?data=invalid-base64!");

        // Act
        var result = _controller.DiscordAuth();

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequest = result as BadRequestObjectResult;
        badRequest!.Value.Should().Be("Invalid request");
    }

    [Fact]
    public void DiscordAuth_ShouldReturnBadRequest_WhenDataContainsInvalidJson()
    {
        // Arrange
        var invalidJson = "{ invalid json }";
        var invalidBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(invalidJson));
        _controller.ControllerContext.HttpContext.Request.QueryString = new QueryString($"?data={invalidBase64}");

        // Act
        var result = _controller.DiscordAuth();

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequest = result as BadRequestObjectResult;
        badRequest!.Value.Should().Be("Invalid request");
    }

    [Fact]
    public void DiscordAuth_ShouldReturnHtmlContent_WhenValidDataProvided()
    {
        // Arrange
        var userRegister = new UserRegister
        {
            GameAccountId = 123456789,
            UserLocalContentId = 987654321,
            Name = "TestUser",
            ClientId = "test-client",
            Version = "1.0.0"
        };

        var jsonString = JsonSerializer.Serialize(userRegister);
        var base64Data = Convert.ToBase64String(Encoding.UTF8.GetBytes(jsonString));
        _controller.ControllerContext.HttpContext.Request.QueryString = new QueryString($"?data={base64Data}");

        // Act
        var result = _controller.DiscordAuth();

        // Assert
        result.Should().BeOfType<ContentResult>();
        var contentResult = result as ContentResult;
        contentResult!.ContentType.Should().Be("text/html");
        contentResult.Content.Should().Contain("Welcome to AlphaScope!");
        contentResult.Content.Should().Contain("TestUser");
        contentResult.Content.Should().Contain("123456789");
        contentResult.Content.Should().Contain("Complete Login");
    }

    [Fact]
    public void DiscordAuth_ShouldHandleDataFromRawQueryString_WhenNotInQueryParams()
    {
        // Arrange
        var userRegister = new UserRegister
        {
            GameAccountId = 123456789,
            UserLocalContentId = 987654321,
            Name = "TestUser",
            ClientId = "test-client",
            Version = "1.0.0"
        };

        var jsonString = JsonSerializer.Serialize(userRegister);
        var base64Data = Convert.ToBase64String(Encoding.UTF8.GetBytes(jsonString));
        
        // Simulate raw query string without proper parameter parsing
        _controller.ControllerContext.HttpContext.Request.QueryString = new QueryString($"?{base64Data}");

        // Act
        var result = _controller.DiscordAuth();

        // Assert
        result.Should().BeOfType<ContentResult>();
        var contentResult = result as ContentResult;
        contentResult!.ContentType.Should().Be("text/html");
        contentResult.Content.Should().Contain("Welcome to AlphaScope!");
    }

    [Fact]
    public async Task CompleteLogin_ShouldReturnBadRequest_WhenSessionNotFound()
    {
        // Arrange
        var nonExistentData = "non-existent-session-key";

        // Act
        var result = await _controller.CompleteLogin(nonExistentData);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequest = result as BadRequestObjectResult;
        badRequest!.Value.Should().Be("Invalid or expired login session");
    }

    [Fact]
    public async Task CompleteLogin_ShouldCreateNewUser_WhenUserDoesNotExist()
    {
        // Arrange
        var userRegister = new UserRegister
        {
            GameAccountId = 123456789,
            UserLocalContentId = 987654321,
            Name = "TestUser",
            ClientId = "test-client",
            Version = "1.0.0"
        };

        var jsonString = JsonSerializer.Serialize(userRegister);
        var base64Data = Convert.ToBase64String(Encoding.UTF8.GetBytes(jsonString));

        // First call DiscordAuth to establish the session
        _controller.ControllerContext.HttpContext.Request.QueryString = new QueryString($"?data={base64Data}");
        var discordAuthResult = _controller.DiscordAuth();
        discordAuthResult.Should().BeOfType<ContentResult>();

        // Act
        var result = await _controller.CompleteLogin(base64Data);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().BeEquivalentTo(new { message = "Login completed successfully" });

        // Verify user was created in database
        var dbUser = await _context.Users.FirstOrDefaultAsync(u => u.GameAccountId == 123456789);
        dbUser.Should().NotBeNull();
        dbUser!.Name.Should().Be("TestUser");
        dbUser.PrimaryCharacterLocalContentId.Should().Be(987654321);
        dbUser.AppRoleId.Should().Be((int)UserRole.Member);
        dbUser.ApiKey.Should().EndWith("-123456789");
        dbUser.BaseUrl.Should().Be("https://localhost:5001/v1/");
    }

    [Fact]
    public async Task CompleteLogin_ShouldUpdateExistingUser_WhenUserExists()
    {
        // Arrange
        var existingUser = new ApplicationUser
        {
            GameAccountId = 123456789,
            PrimaryCharacterLocalContentId = 987654321,
            Name = "ExistingUser",
            ApiKey = "test-key-123456789",
            AppRoleId = (int)UserRole.Member,
            BaseUrl = "https://localhost:5001/v1/",
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            LastLoginAt = DateTime.UtcNow.AddHours(-2)
        };

        _context.Users.Add(existingUser);
        await _context.SaveChangesAsync();

        var userRegister = new UserRegister
        {
            GameAccountId = 123456789,
            UserLocalContentId = 987654321,
            Name = "TestUser", // Different name, should keep existing user's name
            ClientId = "test-client",
            Version = "1.0.0"
        };

        var jsonString = JsonSerializer.Serialize(userRegister);
        var base64Data = Convert.ToBase64String(Encoding.UTF8.GetBytes(jsonString));

        // First call DiscordAuth to establish the session
        _controller.ControllerContext.HttpContext.Request.QueryString = new QueryString($"?data={base64Data}");
        var discordAuthResult = _controller.DiscordAuth();
        discordAuthResult.Should().BeOfType<ContentResult>();

        // Act
        var result = await _controller.CompleteLogin(base64Data);

        // Assert
        result.Should().BeOfType<OkObjectResult>();

        // Verify LastLoginAt was updated
        var updatedUser = await _context.Users.FindAsync(existingUser.Id);
        updatedUser!.LastLoginAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        updatedUser.Name.Should().Be("ExistingUser"); // Should keep existing name
    }

    [Fact]
    public async Task CompleteLogin_ShouldGenerateValidApiKey_ForNewUser()
    {
        // Arrange
        var userRegister = new UserRegister
        {
            GameAccountId = 123456789,
            UserLocalContentId = 987654321,
            Name = "TestUser",
            ClientId = "test-client",
            Version = "1.0.0"
        };

        var jsonString = JsonSerializer.Serialize(userRegister);
        var base64Data = Convert.ToBase64String(Encoding.UTF8.GetBytes(jsonString));

        // First call DiscordAuth to establish the session
        _controller.ControllerContext.HttpContext.Request.QueryString = new QueryString($"?data={base64Data}");
        var discordAuthResult = _controller.DiscordAuth();
        discordAuthResult.Should().BeOfType<ContentResult>();

        // Act
        var result = await _controller.CompleteLogin(base64Data);

        // Assert
        result.Should().BeOfType<OkObjectResult>();

        var dbUser = await _context.Users.FirstOrDefaultAsync(u => u.GameAccountId == 123456789);
        dbUser!.ApiKey.Should().NotBeNullOrEmpty();
        dbUser.ApiKey.Should().EndWith("-123456789");
        dbUser.ApiKey.Should().MatchRegex(@"^[a-f0-9]+-123456789$");
    }

    [Fact]
    public async Task CompleteLogin_ShouldSetCorrectTimestamps_ForNewUser()
    {
        // Arrange
        var beforeLogin = DateTime.UtcNow;
        var userRegister = new UserRegister
        {
            GameAccountId = 123456789,
            UserLocalContentId = 987654321,
            Name = "TestUser",
            ClientId = "test-client",
            Version = "1.0.0"
        };

        var jsonString = JsonSerializer.Serialize(userRegister);
        var base64Data = Convert.ToBase64String(Encoding.UTF8.GetBytes(jsonString));

        // First call DiscordAuth to establish the session
        _controller.ControllerContext.HttpContext.Request.QueryString = new QueryString($"?data={base64Data}");
        var discordAuthResult = _controller.DiscordAuth();
        discordAuthResult.Should().BeOfType<ContentResult>();

        // Act
        var result = await _controller.CompleteLogin(base64Data);
        var afterLogin = DateTime.UtcNow;

        // Assert
        result.Should().BeOfType<OkObjectResult>();

        var dbUser = await _context.Users.FirstOrDefaultAsync(u => u.GameAccountId == 123456789);
        dbUser!.CreatedAt.Should().BeOnOrAfter(beforeLogin).And.BeOnOrBefore(afterLogin);
        dbUser.LastLoginAt.Should().BeOnOrAfter(beforeLogin).And.BeOnOrBefore(afterLogin);
    }

    [Fact]
    public async Task CompleteLogin_ShouldSetBaseUrlFromRequest()
    {
        // Arrange
        _controller.ControllerContext.HttpContext.Request.Scheme = "http";
        _controller.ControllerContext.HttpContext.Request.Host = new HostString("example.com:8080");

        var userRegister = new UserRegister
        {
            GameAccountId = 123456789,
            UserLocalContentId = 987654321,
            Name = "TestUser",
            ClientId = "test-client",
            Version = "1.0.0"
        };

        var jsonString = JsonSerializer.Serialize(userRegister);
        var base64Data = Convert.ToBase64String(Encoding.UTF8.GetBytes(jsonString));

        // First call DiscordAuth to establish the session
        _controller.ControllerContext.HttpContext.Request.QueryString = new QueryString($"?data={base64Data}");
        var discordAuthResult = _controller.DiscordAuth();
        discordAuthResult.Should().BeOfType<ContentResult>();

        // Act
        var result = await _controller.CompleteLogin(base64Data);

        // Assert
        result.Should().BeOfType<OkObjectResult>();

        var dbUser = await _context.Users.FirstOrDefaultAsync(u => u.GameAccountId == 123456789);
        dbUser!.BaseUrl.Should().Be("http://example.com:8080/v1/");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public async Task CompleteLogin_ShouldHandleVariousGameAccountIds(int gameAccountId)
    {
        // Arrange
        var userRegister = new UserRegister
        {
            GameAccountId = gameAccountId,
            UserLocalContentId = 987654321,
            Name = "TestUser",
            ClientId = "test-client",
            Version = "1.0.0"
        };

        var jsonString = JsonSerializer.Serialize(userRegister);
        var base64Data = Convert.ToBase64String(Encoding.UTF8.GetBytes(jsonString));

        // First call DiscordAuth to establish the session
        _controller.ControllerContext.HttpContext.Request.QueryString = new QueryString($"?data={base64Data}");
        var discordAuthResult = _controller.DiscordAuth();
        discordAuthResult.Should().BeOfType<ContentResult>();

        // Act
        var result = await _controller.CompleteLogin(base64Data);

        // Assert
        result.Should().BeOfType<OkObjectResult>();

        var dbUser = await _context.Users.FirstOrDefaultAsync(u => u.GameAccountId == gameAccountId);
        dbUser.Should().NotBeNull();
        dbUser!.GameAccountId.Should().Be(gameAccountId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("A")]
    [InlineData("VeryLongPlayerNameThatMightExceedLimits")]
    [InlineData("Player With Spaces")]
    [InlineData("Player@#$%^&*()")]
    public async Task CompleteLogin_ShouldHandleVariousPlayerNames(string playerName)
    {
        // Arrange
        var userRegister = new UserRegister
        {
            GameAccountId = 123456789,
            UserLocalContentId = 987654321,
            Name = playerName,
            ClientId = "test-client",
            Version = "1.0.0"
        };

        var jsonString = JsonSerializer.Serialize(userRegister);
        var base64Data = Convert.ToBase64String(Encoding.UTF8.GetBytes(jsonString));

        // First call DiscordAuth to establish the session
        _controller.ControllerContext.HttpContext.Request.QueryString = new QueryString($"?data={base64Data}");
        var discordAuthResult = _controller.DiscordAuth();
        discordAuthResult.Should().BeOfType<ContentResult>();

        // Act
        var result = await _controller.CompleteLogin(base64Data);

        // Assert
        result.Should().BeOfType<OkObjectResult>();

        var dbUser = await _context.Users.FirstOrDefaultAsync(u => u.GameAccountId == 123456789);
        dbUser.Should().NotBeNull();
        dbUser!.Name.Should().Be(playerName);
    }

    [Fact]
    public async Task WaitForLogin_ShouldSetCorrectHeaders()
    {
        // Arrange
        var userRegister = new UserRegister
        {
            GameAccountId = 123456789,
            UserLocalContentId = 987654321,
            Name = "TestUser",
            ClientId = "test-client",
            Version = "1.0.0"
        };

        var jsonString = JsonSerializer.Serialize(userRegister);
        var base64Data = Convert.ToBase64String(Encoding.UTF8.GetBytes(jsonString));

        // First call DiscordAuth to establish the session
        _controller.ControllerContext.HttpContext.Request.QueryString = new QueryString($"?data={base64Data}");
        var discordAuthResult = _controller.DiscordAuth();
        discordAuthResult.Should().BeOfType<ContentResult>();

        // Act - Just test that the method doesn't throw by starting and cancelling quickly
        var cancellationToken = new CancellationTokenSource(TimeSpan.FromMilliseconds(50)).Token;
        
        // This will timeout quickly since no one completes the login, but it tests the basic setup
        try
        {
            await _controller.WaitForLogin(base64Data, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation token expires
        }

        // Assert - Verify headers were set
        var response = _controller.ControllerContext.HttpContext.Response;
        response.Headers.Should().ContainKey("Content-Type");
        response.Headers["Content-Type"].ToString().Should().Be("text/event-stream");
    }

    [Fact]
    public async Task AuthWorkflow_ShouldCompleteFullFlow_Successfully()
    {
        // Arrange
        var userRegister = new UserRegister
        {
            GameAccountId = 123456789,
            UserLocalContentId = 987654321,
            Name = "TestUser",
            ClientId = "test-client",
            Version = "1.0.0"
        };

        var jsonString = JsonSerializer.Serialize(userRegister);
        var base64Data = Convert.ToBase64String(Encoding.UTF8.GetBytes(jsonString));

        // Act 1: Call DiscordAuth to establish session
        _controller.ControllerContext.HttpContext.Request.QueryString = new QueryString($"?data={base64Data}");
        var discordAuthResult = _controller.DiscordAuth();

        // Assert 1: DiscordAuth should return HTML
        discordAuthResult.Should().BeOfType<ContentResult>();
        var contentResult = discordAuthResult as ContentResult;
        contentResult!.Content.Should().Contain("TestUser");

        // Act 2: Complete the login
        var completeResult = await _controller.CompleteLogin(base64Data);

        // Assert 2: Login should complete successfully
        completeResult.Should().BeOfType<OkObjectResult>();

        // Assert 3: User should be created in database
        var dbUser = await _context.Users.FirstOrDefaultAsync(u => u.GameAccountId == 123456789);
        dbUser.Should().NotBeNull();
        dbUser!.Name.Should().Be("TestUser");
        dbUser.ApiKey.Should().NotBeNullOrEmpty();
    }
}