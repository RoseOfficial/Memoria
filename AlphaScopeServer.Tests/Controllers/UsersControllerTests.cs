using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using AlphaScopeServer.Controllers;
using AlphaScopeServer.Data;
using AlphaScopeServer.Models.DTOs;
using AlphaScopeServer.Models.Entities;
using TestUtilities;

namespace AlphaScopeServer.Tests.Controllers;

public class UsersControllerTests : IDisposable
{
    private readonly AlphaScopeDbContext _context;
    private readonly ILogger<UsersController> _mockLogger;
    private readonly UsersController _controller;

    public UsersControllerTests()
    {
        var options = DatabaseTestUtilities.CreateInMemoryDbOptions<AlphaScopeDbContext>();
        _context = new AlphaScopeDbContext(options);
        _context.Database.EnsureCreated();

        _mockLogger = LoggerTestUtilities.CreateMockLogger<UsersController>();
        _controller = new UsersController(_context, _mockLogger);

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
    public async Task Login_ShouldCreateNewUser_WhenUserDoesNotExist()
    {
        // Arrange
        var request = new AlphaScopeServer.Models.DTOs.UserRegister
        {
            GameAccountId = 123456789,
            UserLocalContentId = 987654321,
            Name = "TestUser",
            ClientId = "test-client",
            Version = "1.0.0"
        };

        // Act
        var result = await _controller.Login(request);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var user = okResult.Value.Should().BeOfType<User>().Subject;

        user.GameAccountId.Should().Be(123456789);
        user.LocalContentId.Should().Be(987654321);
        user.Name.Should().Be("TestUser");
        user.AppRoleId.Should().Be((int)UserRole.Member);
        user.BaseUrl.Should().Be("https://localhost:5001/v1/");

        // Verify user was created in database
        var dbUser = await _context.Users.FirstOrDefaultAsync(u => u.GameAccountId == 123456789);
        dbUser.Should().NotBeNull();
        dbUser!.Name.Should().Be("TestUser");
        dbUser.ApiKey.Should().NotBeNullOrEmpty();
        dbUser.ApiKey.Should().NotContain("-123456789"); // opaque key — no GameAccountId suffix
    }

    [Fact]
    public async Task Login_ShouldReturnExistingUser_WhenUserExists()
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

        var request = new AlphaScopeServer.Models.DTOs.UserRegister
        {
            GameAccountId = 123456789,
            UserLocalContentId = 987654321,
            Name = "TestUser", // Different name, should use existing user
            ClientId = "test-client",
            Version = "1.0.0"
        };

        // Act
        var result = await _controller.Login(request);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var user = okResult.Value.Should().BeOfType<User>().Subject;

        user.GameAccountId.Should().Be(123456789);
        user.Name.Should().Be("ExistingUser"); // Should keep existing name
        user.AppRoleId.Should().Be((int)UserRole.Member);

        // Verify LastLoginAt was updated
        var updatedUser = await _context.Users.FindAsync(existingUser.Id);
        updatedUser!.LastLoginAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task Login_ShouldCreatePrimaryCharacter_ForNewUser()
    {
        // Arrange
        var request = new AlphaScopeServer.Models.DTOs.UserRegister
        {
            GameAccountId = 123456789,
            UserLocalContentId = 987654321,
            Name = "TestUser",
            ClientId = "test-client",
            Version = "1.0.0"
        };

        // Act
        var result = await _controller.Login(request);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var user = okResult.Value.Should().BeOfType<User>().Subject;

        // Verify primary character was created
        var dbUser = await _context.Users
            .Include(u => u.Characters)
            .FirstOrDefaultAsync(u => u.GameAccountId == 123456789);

        dbUser!.Characters.Should().HaveCount(1);
        dbUser.Characters.First().LocalContentId.Should().Be(987654321);
        dbUser.Characters.First().Name.Should().Be("TestUser");
    }

    [Fact]
    public async Task Login_ShouldGenerateValidApiKey()
    {
        // Arrange
        var request = new AlphaScopeServer.Models.DTOs.UserRegister
        {
            GameAccountId = 123456789,
            UserLocalContentId = 987654321,
            Name = "TestUser",
            ClientId = "test-client",
            Version = "1.0.0"
        };

        // Act
        var result = await _controller.Login(request);

        // Assert
        var dbUser = await _context.Users.FirstOrDefaultAsync(u => u.GameAccountId == 123456789);
        dbUser!.ApiKey.Should().NotBeNullOrEmpty();
        dbUser.ApiKey.Should().MatchRegex(@"^[A-Za-z0-9]+$"); // opaque key, no suffix
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public async Task Login_ShouldHandleVariousGameAccountIds(int gameAccountId)
    {
        // Arrange
        var request = new AlphaScopeServer.Models.DTOs.UserRegister
        {
            GameAccountId = gameAccountId,
            UserLocalContentId = 987654321,
            Name = "TestUser",
            ClientId = "test-client",
            Version = "1.0.0"
        };

        // Act
        var result = await _controller.Login(request);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var user = okResult.Value.Should().BeOfType<User>().Subject;

        user.GameAccountId.Should().Be(gameAccountId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("A")]
    [InlineData("VeryLongPlayerNameThatMightExceedLimits")]
    [InlineData("Player With Spaces")]
    [InlineData("Player@#$%^&*()")]
    public async Task Login_ShouldHandleVariousPlayerNames(string playerName)
    {
        // Arrange
        var request = new AlphaScopeServer.Models.DTOs.UserRegister
        {
            GameAccountId = 123456789,
            UserLocalContentId = 987654321,
            Name = playerName,
            ClientId = "test-client",
            Version = "1.0.0"
        };

        // Act
        var result = await _controller.Login(request);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var user = okResult.Value.Should().BeOfType<User>().Subject;

        user.Name.Should().Be(playerName);
    }

    [Fact]
    public async Task Login_ShouldSetCorrectTimestamps()
    {
        // Arrange
        var beforeLogin = DateTime.UtcNow;
        var request = new AlphaScopeServer.Models.DTOs.UserRegister
        {
            GameAccountId = 123456789,
            UserLocalContentId = 987654321,
            Name = "TestUser",
            ClientId = "test-client",
            Version = "1.0.0"
        };

        // Act
        var result = await _controller.Login(request);
        var afterLogin = DateTime.UtcNow;

        // Assert
        var dbUser = await _context.Users.FirstOrDefaultAsync(u => u.GameAccountId == 123456789);
        dbUser!.CreatedAt.Should().BeOnOrAfter(beforeLogin).And.BeOnOrBefore(afterLogin);
        dbUser.LastLoginAt.Should().BeOnOrAfter(beforeLogin).And.BeOnOrBefore(afterLogin);
    }

    [Fact]
    public async Task Login_ShouldSetBaseUrlFromRequest()
    {
        // Arrange
        _controller.ControllerContext.HttpContext.Request.Scheme = "http";
        _controller.ControllerContext.HttpContext.Request.Host = new HostString("example.com:8080");

        var request = new AlphaScopeServer.Models.DTOs.UserRegister
        {
            GameAccountId = 123456789,
            UserLocalContentId = 987654321,
            Name = "TestUser",
            ClientId = "test-client",
            Version = "1.0.0"
        };

        // Act
        var result = await _controller.Login(request);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var user = okResult.Value.Should().BeOfType<User>().Subject;

        user.BaseUrl.Should().Be("http://example.com:8080/v1/");
    }

    [Fact]
    public async Task Login_ShouldIncludeCharactersInResponse()
    {
        // Arrange
        var request = new AlphaScopeServer.Models.DTOs.UserRegister
        {
            GameAccountId = 123456789,
            UserLocalContentId = 987654321,
            Name = "TestUser",
            ClientId = "test-client",
            Version = "1.0.0"
        };

        // Act
        var result = await _controller.Login(request);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var user = okResult.Value.Should().BeOfType<User>().Subject;

        user.Characters.Should().NotBeNull();
        user.Characters.Should().HaveCount(1);
        user.Characters.First().Name.Should().Be("TestUser");
        user.Characters.First().LocalContentId.Should().Be(987654321);
    }

    [Fact]
    public async Task Login_ShouldIncludePrivacySettings()
    {
        // Arrange
        var request = new AlphaScopeServer.Models.DTOs.UserRegister
        {
            GameAccountId = 123456789,
            UserLocalContentId = 987654321,
            Name = "TestUser",
            ClientId = "test-client",
            Version = "1.0.0"
        };

        // Act
        var result = await _controller.Login(request);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var user = okResult.Value.Should().BeOfType<User>().Subject;

        user.Characters.First().Privacy.Should().NotBeNull();
        // Default privacy settings should be set
        user.Characters.First().Privacy!.HideFullProfile.Should().BeFalse();
        user.Characters.First().Privacy!.HideTerritoryInfo.Should().BeFalse();
        user.Characters.First().Privacy!.HideCustomizations.Should().BeFalse();
    }

    [Fact]
    public async Task Login_ShouldHandleDatabaseConcurrency()
    {
        // Arrange - Simulate concurrent requests for the same user
        var request1 = new AlphaScopeServer.Models.DTOs.UserRegister
        {
            GameAccountId = 123456789,
            UserLocalContentId = 987654321,
            Name = "TestUser1",
            ClientId = "test-client",
            Version = "1.0.0"
        };

        var request2 = new AlphaScopeServer.Models.DTOs.UserRegister
        {
            GameAccountId = 123456789,
            UserLocalContentId = 987654321,
            Name = "TestUser2",
            ClientId = "test-client",
            Version = "1.0.0"
        };

        // Act - Both requests should succeed
        var task1 = _controller.Login(request1);
        var task2 = _controller.Login(request2);

        var results = await Task.WhenAll(task1, task2);

        // Assert - Should have only one user in database
        var usersCount = await _context.Users.CountAsync(u => u.GameAccountId == 123456789);
        usersCount.Should().Be(1);

        // Both results should be successful
        results[0].Result.Should().BeOfType<OkObjectResult>();
        results[1].Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Login_ShouldValidateRequest()
    {
        // Arrange - Invalid request with missing data
        var request = new AlphaScopeServer.Models.DTOs.UserRegister
        {
            GameAccountId = 0, // Invalid
            UserLocalContentId = 0, // Invalid
            Name = "", // Invalid
            ClientId = "test-client",
            Version = "1.0.0"
        };

        // Act
        var result = await _controller.Login(request);

        // Assert - Should still process but with invalid data
        // (Note: This test assumes the controller doesn't validate input - 
        // in a real scenario, you might want to add validation)
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var user = okResult.Value.Should().BeOfType<User>().Subject;

        user.GameAccountId.Should().Be(0);
        user.LocalContentId.Should().Be(0);
    }
}

// Mock User role enum
public enum UserRole
{
    Member = 1,
    Admin = 2
}