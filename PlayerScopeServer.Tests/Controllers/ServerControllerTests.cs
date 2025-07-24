using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using PlayerScopeServer.Controllers;
using PlayerScopeServer.Data;
using PlayerScopeServer.Models.DTOs;
using PlayerScopeServer.Models.Entities;
using TestUtilities;

namespace PlayerScopeServer.Tests.Controllers;

public class ServerControllerTests : IDisposable
{
    private readonly PlayerScopeDbContext _context;
    private readonly ILogger<ServerController> _mockLogger;
    private readonly ServerController _controller;

    public ServerControllerTests()
    {
        var options = DatabaseTestUtilities.CreateInMemoryDbOptions<PlayerScopeDbContext>();
        _context = new PlayerScopeDbContext(options);
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

        var retainers = new List<Retainer>
        {
            new() { LocalContentId = 1, Name = "Retainer1", WorldId = 65, OwnerLocalContentId = 1, Owner = players[0] },
            new() { LocalContentId = 2, Name = "Retainer2", WorldId = 66, OwnerLocalContentId = 2, Owner = players[1] },
            new() { LocalContentId = 3, Name = "Retainer3", WorldId = 67, OwnerLocalContentId = 1, Owner = players[0] },
            new() { LocalContentId = 4, Name = "Retainer4", WorldId = 68, OwnerLocalContentId = 1, Owner = players[0] }
        };

        var users = new List<ApplicationUser>
        {
            new() { GameAccountId = 123, Name = "User1" },
            new() { GameAccountId = 456, Name = "User2" }
        };

        _context.Players.AddRange(players);
        _context.Retainers.AddRange(retainers);
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
        stats.TotalRetainerCount.Should().Be(4);
        stats.TotalPrivateRetainerCount.Should().Be(1); // Retainer2 owned by private Player2
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
    public async Task GetServerStats_ShouldCalculatePrivateRetainersCorrectly()
    {
        // Arrange
        var publicOwner = new Player { LocalContentId = 1, Name = "PublicOwner", CurrentWorldId = 65, IsPrivate = false };
        var privateOwner = new Player { LocalContentId = 2, Name = "PrivateOwner", CurrentWorldId = 66, IsPrivate = true };

        var retainers = new List<Retainer>
        {
            new() { LocalContentId = 1, Name = "PublicRetainer1", WorldId = 65, OwnerLocalContentId = 1, Owner = publicOwner },
            new() { LocalContentId = 2, Name = "PublicRetainer2", WorldId = 65, OwnerLocalContentId = 1, Owner = publicOwner },
            new() { LocalContentId = 3, Name = "PrivateRetainer1", WorldId = 66, OwnerLocalContentId = 2, Owner = privateOwner },
            new() { LocalContentId = 4, Name = "PrivateRetainer2", WorldId = 66, OwnerLocalContentId = 2, Owner = privateOwner }
        };

        _context.Players.AddRange(publicOwner, privateOwner);
        _context.Retainers.AddRange(retainers);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetServerStats();

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var stats = okResult.Value.Should().BeOfType<ServerStatsDto>().Subject;

        stats.TotalPlayerCount.Should().Be(2);
        stats.TotalPrivatePlayerCount.Should().Be(1);
        stats.TotalRetainerCount.Should().Be(4);
        stats.TotalPrivateRetainerCount.Should().Be(2); // Two retainers owned by private owner
    }

    [Fact]
    public async Task GetPlayerRetainerStats_ShouldReturnCorrectWorldStats_WhenDataExists()
    {
        // Arrange
        var players = new List<Player>
        {
            new() { LocalContentId = 1, Name = "Player1", HomeWorldId = 65, CurrentWorldId = 65, IsPrivate = false },
            new() { LocalContentId = 2, Name = "Player2", HomeWorldId = 65, CurrentWorldId = 66, IsPrivate = false },
            new() { LocalContentId = 3, Name = "Player3", HomeWorldId = 66, CurrentWorldId = 67, IsPrivate = false },
            new() { LocalContentId = 4, Name = "Player4", HomeWorldId = null, CurrentWorldId = 68, IsPrivate = false } // No home world
        };

        var retainers = new List<Retainer>
        {
            new() { LocalContentId = 1, Name = "Retainer1", WorldId = 65, OwnerLocalContentId = 1, Owner = players[0] },
            new() { LocalContentId = 2, Name = "Retainer2", WorldId = 65, OwnerLocalContentId = 1, Owner = players[0] },
            new() { LocalContentId = 3, Name = "Retainer3", WorldId = 66, OwnerLocalContentId = 2, Owner = players[1] },
            new() { LocalContentId = 4, Name = "Retainer4", WorldId = 67, OwnerLocalContentId = 3, Owner = players[2] }
        };

        _context.Players.AddRange(players);
        _context.Retainers.AddRange(retainers);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetPlayerRetainerStats();

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var stats = okResult.Value.Should().BeOfType<ServerPlayerAndRetainerStatsDto>().Subject;

        // Player world stats (only players with HomeWorldId)
        stats.PlayerWorldStats.Should().HaveCount(2);
        stats.PlayerWorldStats.Should().Contain(s => s.WorldId == 65 && s.Count == 2);
        stats.PlayerWorldStats.Should().Contain(s => s.WorldId == 66 && s.Count == 1);

        // Retainer world stats
        stats.RetainerWorldStats.Should().HaveCount(3);
        stats.RetainerWorldStats.Should().Contain(s => s.WorldId == 65 && s.Count == 2);
        stats.RetainerWorldStats.Should().Contain(s => s.WorldId == 66 && s.Count == 1);
        stats.RetainerWorldStats.Should().Contain(s => s.WorldId == 67 && s.Count == 1);

        stats.LastUpdate.Should().BeGreaterThan(0);
        
        // Verify timestamp is recent
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        stats.LastUpdate.Should().BeCloseTo(now, 60);
    }

    [Fact]
    public async Task GetPlayerRetainerStats_ShouldReturnEmptyStats_WhenNoDataExists()
    {
        // Arrange
        // No data added to the context

        // Act
        var result = await _controller.GetPlayerRetainerStats();

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var stats = okResult.Value.Should().BeOfType<ServerPlayerAndRetainerStatsDto>().Subject;

        stats.PlayerWorldStats.Should().BeEmpty();
        stats.RetainerWorldStats.Should().BeEmpty();
        stats.LastUpdate.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetPlayerRetainerStats_ShouldExcludePlayersWithoutHomeWorld()
    {
        // Arrange
        var players = new List<Player>
        {
            new() { LocalContentId = 1, Name = "Player1", HomeWorldId = 65, CurrentWorldId = 65, IsPrivate = false },
            new() { LocalContentId = 2, Name = "Player2", HomeWorldId = null, CurrentWorldId = 66, IsPrivate = false },
            new() { LocalContentId = 3, Name = "Player3", HomeWorldId = null, CurrentWorldId = 67, IsPrivate = false }
        };

        _context.Players.AddRange(players);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetPlayerRetainerStats();

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var stats = okResult.Value.Should().BeOfType<ServerPlayerAndRetainerStatsDto>().Subject;

        stats.PlayerWorldStats.Should().HaveCount(1);
        stats.PlayerWorldStats.Should().Contain(s => s.WorldId == 65 && s.Count == 1);
    }

    [Fact]
    public async Task GetPlayerRetainerStats_ShouldGroupWorldStatsCorrectly()
    {
        // Arrange
        var players = new List<Player>();
        var retainers = new List<Retainer>();

        // Create multiple players/retainers for the same worlds
        for (int i = 1; i <= 5; i++)
        {
            var player = new Player 
            { 
                LocalContentId = i, 
                Name = $"Player{i}", 
                HomeWorldId = 65, // All on world 65
                CurrentWorldId = 65, 
                IsPrivate = false 
            };
            players.Add(player);

            for (int j = 1; j <= 2; j++)
            {
                var retainerId = (i - 1) * 2 + j;
                retainers.Add(new Retainer 
                { 
                    LocalContentId = retainerId, 
                    Name = $"Retainer{retainerId}", 
                    WorldId = 65, // All on world 65
                    OwnerLocalContentId = i, 
                    Owner = player 
                });
            }
        }

        _context.Players.AddRange(players);
        _context.Retainers.AddRange(retainers);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetPlayerRetainerStats();

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var stats = okResult.Value.Should().BeOfType<ServerPlayerAndRetainerStatsDto>().Subject;

        stats.PlayerWorldStats.Should().HaveCount(1);
        stats.PlayerWorldStats.First().WorldId.Should().Be(65);
        stats.PlayerWorldStats.First().Count.Should().Be(5);

        stats.RetainerWorldStats.Should().HaveCount(1);
        stats.RetainerWorldStats.First().WorldId.Should().Be(65);
        stats.RetainerWorldStats.First().Count.Should().Be(10);
    }

    [Fact]
    public async Task GetPlayerRetainerStats_ShouldMapWorldCountStatCorrectly()
    {
        // Arrange
        var player = new Player { LocalContentId = 1, Name = "Player1", HomeWorldId = 12345, CurrentWorldId = 65, IsPrivate = false };
        var retainer = new Retainer { LocalContentId = 1, Name = "Retainer1", WorldId = 12321, OwnerLocalContentId = 1, Owner = player };

        _context.Players.Add(player);
        _context.Retainers.Add(retainer);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetPlayerRetainerStats();

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var stats = okResult.Value.Should().BeOfType<ServerPlayerAndRetainerStatsDto>().Subject;

        stats.PlayerWorldStats.Should().HaveCount(1);
        var playerStat = stats.PlayerWorldStats.First();
        playerStat.WorldId.Should().Be(12345);
        playerStat.Count.Should().Be(1);

        stats.RetainerWorldStats.Should().HaveCount(1);
        var retainerStat = stats.RetainerWorldStats.First();
        retainerStat.WorldId.Should().Be(12321);
        retainerStat.Count.Should().Be(1);
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
        var retainers = new List<Retainer>();
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

        // Create retainers - 200 for public owners, 100 for private owners
        for (int i = 1; i <= 200; i++)
        {
            var ownerId = ((i - 1) % 100) + 1; // Distribute among public players
            retainers.Add(new Retainer 
            { 
                LocalContentId = i, 
                Name = $"PublicRetainer{i}", 
                WorldId = 65, 
                OwnerLocalContentId = ownerId,
                Owner = players.First(p => p.LocalContentId == ownerId)
            });
        }

        for (int i = 201; i <= 300; i++)
        {
            var ownerId = ((i - 201) % 50) + 101; // Distribute among private players
            retainers.Add(new Retainer 
            { 
                LocalContentId = i, 
                Name = $"PrivateRetainer{i}", 
                WorldId = 65, 
                OwnerLocalContentId = ownerId,
                Owner = players.First(p => p.LocalContentId == ownerId)
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
        _context.Retainers.AddRange(retainers);
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
        stats.TotalRetainerCount.Should().Be(300);
        stats.TotalPrivateRetainerCount.Should().Be(100);
        stats.TotalUserCount.Should().Be(25);
    }
}