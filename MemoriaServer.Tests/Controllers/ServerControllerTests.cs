using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using MemoriaServer.Controllers;
using MemoriaServer.Data;
using MemoriaServer.Models.DTOs;
using MemoriaServer.Models.Entities;
using TestUtilities;

namespace MemoriaServer.Tests.Controllers;

public class ServerControllerTests : IDisposable
{
    private readonly MemoriaDbContext _context;
    private readonly ILogger<ServerController> _mockLogger;
    private readonly ServerController _controller;

    public ServerControllerTests()
    {
        var options = DatabaseTestUtilities.CreateInMemoryDbOptions<MemoriaDbContext>();
        _context = new MemoriaDbContext(options);
        _context.Database.EnsureCreated();

        _mockLogger = LoggerTestUtilities.CreateMockLogger<ServerController>();
        _controller = new ServerController(_context, _mockLogger);

        // Setup HttpContext
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    public void Dispose()
    {
        _context?.Dispose();
    }

    [Fact]
    public async Task GetServerStatus_ShouldReturnOnlineStatus_WhenDatabaseIsConnected()
    {
        // Arrange
        // Database is already connected in the constructor

        // Act
        var result = await _controller.GetServerStatus();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        
        var response = okResult!.Value;
        response.Should().NotBeNull();
        
        // Use reflection to check the anonymous object properties
        var statusProperty = response!.GetType().GetProperty("status");
        statusProperty!.GetValue(response).Should().Be("online");
        
        var versionProperty = response.GetType().GetProperty("version");
        versionProperty!.GetValue(response).Should().Be("v1.2.0");
        
        var timestampProperty = response.GetType().GetProperty("timestamp");
        var timestamp = (long)timestampProperty!.GetValue(response)!;
        timestamp.Should().BeGreaterThan(0);
        
        // Verify timestamp is recent (within last minute)
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        timestamp.Should().BeCloseTo(now, 60);
    }

    [Fact]
    public async Task GetServerStats_ShouldReturnCorrectCounts_WhenDataExists()
    {
        // Arrange
        var players = new List<Player>
        {
            new() { LocalContentId = 1, Name = "Player1", CurrentWorldId = 65, IsPrivate = false },
            new() { LocalContentId = 2, Name = "Player2", CurrentWorldId = 66, IsPrivate = true },
            new() { LocalContentId = 3, Name = "Player3", CurrentWorldId = 67, IsPrivate = false }
        };

        var users = new List<ApplicationUser>
        {
            new() { GameAccountId = 123, Name = "User1" },
            new() { GameAccountId = 456, Name = "User2" }
        };

        _context.Players.AddRange(players);
        _context.Users.AddRange(users);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetServerStats();

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var stats = okResult.Value.Should().BeOfType<ServerStatsDto>().Subject;

        stats.TotalPlayerCount.Should().Be(3);
        stats.TotalPrivatePlayerCount.Should().Be(1);
        stats.TotalRetainerCount.Should().Be(0);
        stats.TotalPrivateRetainerCount.Should().Be(0);
        stats.TotalUserCount.Should().Be(2);
        stats.LastUpdate.Should().BeGreaterThan(0);
        
        // Verify timestamp is recent
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        stats.LastUpdate.Should().BeCloseTo(now, 60);
    }

    [Fact]
    public async Task GetServerStats_ShouldReturnZeroCounts_WhenNoDataExists()
    {
        // Arrange
        // No data added to the context

        // Act
        var result = await _controller.GetServerStats();

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var stats = okResult.Value.Should().BeOfType<ServerStatsDto>().Subject;

        stats.TotalPlayerCount.Should().Be(0);
        stats.TotalPrivatePlayerCount.Should().Be(0);
        stats.TotalRetainerCount.Should().Be(0);
        stats.TotalPrivateRetainerCount.Should().Be(0);
        stats.TotalUserCount.Should().Be(0);
        stats.LastUpdate.Should().BeGreaterThan(0);
    }







    [Fact]
    public async Task GetServerStats_ShouldHandleEmptyDatabase()
    {
        // Arrange
        // No data added

        // Act
        var result = await _controller.GetServerStats();

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var stats = okResult.Value.Should().BeOfType<ServerStatsDto>().Subject;

        stats.TotalPlayerCount.Should().Be(0);
        stats.TotalPrivatePlayerCount.Should().Be(0);
        stats.TotalRetainerCount.Should().Be(0);
        stats.TotalPrivateRetainerCount.Should().Be(0);
        stats.TotalUserCount.Should().Be(0);
    }

    [Fact]
    public async Task GetServerStats_ShouldHandleLargeDatasets()
    {
        // Arrange
        var players = new List<Player>();
        var users = new List<ApplicationUser>();

        // Create 100 public players and 50 private players
        for (int i = 1; i <= 100; i++)
        {
            players.Add(new Player 
            { 
                LocalContentId = i, 
                Name = $"PublicPlayer{i}", 
                CurrentWorldId = 65, 
                IsPrivate = false 
            });
        }

        for (int i = 101; i <= 150; i++)
        {
            players.Add(new Player 
            { 
                LocalContentId = i, 
                Name = $"PrivatePlayer{i}", 
                CurrentWorldId = 65, 
                IsPrivate = true 
            });
        }

        // Create 25 users
        for (int i = 1; i <= 25; i++)
        {
            users.Add(new ApplicationUser 
            { 
                GameAccountId = i, 
                Name = $"User{i}" 
            });
        }

        _context.Players.AddRange(players);
        _context.Users.AddRange(users);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetServerStats();

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var stats = okResult.Value.Should().BeOfType<ServerStatsDto>().Subject;

        stats.TotalPlayerCount.Should().Be(150);
        stats.TotalPrivatePlayerCount.Should().Be(50);
        stats.TotalRetainerCount.Should().Be(0);
        stats.TotalPrivateRetainerCount.Should().Be(0);
        stats.TotalUserCount.Should().Be(25);
    }
}