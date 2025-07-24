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

public class PlayersControllerTests : IDisposable
{
    private readonly PlayerScopeDbContext _context;
    private readonly ILogger<PlayersController> _mockLogger;
    private readonly PlayersController _controller;

    public PlayersControllerTests()
    {
        var options = DatabaseTestUtilities.CreateInMemoryDbOptions<PlayerScopeDbContext>();
        _context = new PlayerScopeDbContext(options);
        _context.Database.EnsureCreated();

        _mockLogger = LoggerTestUtilities.CreateMockLogger<PlayersController>();
        _controller = new PlayersController(_context, _mockLogger);

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
    public async Task SearchPlayers_ShouldReturnAllPlayers_WhenNoFiltersApplied()
    {
        // Arrange
        var players = new List<Player>
        {
            new() { LocalContentId = 1, Name = "Player1", CurrentWorldId = 65, IsPrivate = false },
            new() { LocalContentId = 2, Name = "Player2", CurrentWorldId = 66, IsPrivate = false },
            new() { LocalContentId = 3, Name = "Player3", CurrentWorldId = 67, IsPrivate = false }
        };

        _context.Players.AddRange(players);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.SearchPlayers();

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var pagination = okResult.Value.Should().BeOfType<PaginationBase<PlayerSearchDto>>().Subject;
        
        pagination.Data.Should().HaveCount(3);
        pagination.Data.Should().Contain(p => p.Name == "Player1");
        pagination.Data.Should().Contain(p => p.Name == "Player2");
        pagination.Data.Should().Contain(p => p.Name == "Player3");
    }

    [Fact]
    public async Task SearchPlayers_ShouldFilterByLocalContentId()
    {
        // Arrange
        var players = new List<Player>
        {
            new() { LocalContentId = 1, Name = "Player1", CurrentWorldId = 65, IsPrivate = false },
            new() { LocalContentId = 2, Name = "Player2", CurrentWorldId = 66, IsPrivate = false }
        };

        _context.Players.AddRange(players);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.SearchPlayers(LocalContentId: 1);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var pagination = okResult.Value.Should().BeOfType<PaginationBase<PlayerSearchDto>>().Subject;
        
        pagination.Data.Should().HaveCount(1);
        pagination.Data.First().LocalContentId.Should().Be(1);
        pagination.Data.First().Name.Should().Be("Player1");
    }

    [Fact]
    public async Task SearchPlayers_ShouldFilterByExactName()
    {
        // Arrange
        var players = new List<Player>
        {
            new() { LocalContentId = 1, Name = "TestPlayer", CurrentWorldId = 65, IsPrivate = false },
            new() { LocalContentId = 2, Name = "AnotherPlayer", CurrentWorldId = 66, IsPrivate = false },
            new() { LocalContentId = 3, Name = "TestPlayerExtra", CurrentWorldId = 67, IsPrivate = false }
        };

        _context.Players.AddRange(players);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.SearchPlayers(Name: "TestPlayer", F_MatchAnyPartOfName: false);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var pagination = okResult.Value.Should().BeOfType<PaginationBase<PlayerSearchDto>>().Subject;
        
        pagination.Data.Should().HaveCount(1);
        pagination.Data.First().Name.Should().Be("TestPlayer");
    }

    [Fact]
    public async Task SearchPlayers_ShouldFilterByPartialName()
    {
        // Arrange
        var players = new List<Player>
        {
            new() { LocalContentId = 1, Name = "TestPlayer", CurrentWorldId = 65, IsPrivate = false },
            new() { LocalContentId = 2, Name = "AnotherPlayer", CurrentWorldId = 66, IsPrivate = false },
            new() { LocalContentId = 3, Name = "TestPlayerExtra", CurrentWorldId = 67, IsPrivate = false }
        };

        _context.Players.AddRange(players);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.SearchPlayers(Name: "TestPlayer", F_MatchAnyPartOfName: true);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var pagination = okResult.Value.Should().BeOfType<PaginationBase<PlayerSearchDto>>().Subject;
        
        pagination.Data.Should().HaveCount(2);
        pagination.Data.Should().Contain(p => p.Name == "TestPlayer");
        pagination.Data.Should().Contain(p => p.Name == "TestPlayerExtra");
    }

    [Fact]
    public async Task SearchPlayers_ShouldFilterByWorldIds()
    {
        // Arrange
        var players = new List<Player>
        {
            new() { LocalContentId = 1, Name = "Player1", CurrentWorldId = 65, IsPrivate = false },
            new() { LocalContentId = 2, Name = "Player2", CurrentWorldId = 66, IsPrivate = false },
            new() { LocalContentId = 3, Name = "Player3", CurrentWorldId = 67, IsPrivate = false },
            new() { LocalContentId = 4, Name = "Player4", CurrentWorldId = null, IsPrivate = false }
        };

        _context.Players.AddRange(players);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.SearchPlayers(F_WorldIds: "65,66");

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var pagination = okResult.Value.Should().BeOfType<PaginationBase<PlayerSearchDto>>().Subject;
        
        pagination.Data.Should().HaveCount(2);
        pagination.Data.Should().Contain(p => p.Name == "Player1");
        pagination.Data.Should().Contain(p => p.Name == "Player2");
    }

    [Fact]
    public async Task SearchPlayers_ShouldIgnoreInvalidWorldIds()
    {
        // Arrange
        var players = new List<Player>
        {
            new() { LocalContentId = 1, Name = "Player1", CurrentWorldId = 65, IsPrivate = false },
            new() { LocalContentId = 2, Name = "Player2", CurrentWorldId = 66, IsPrivate = false }
        };

        _context.Players.AddRange(players);
        await _context.SaveChangesAsync();

        // Act - Include invalid world IDs
        var result = await _controller.SearchPlayers(F_WorldIds: "65,invalid,66,abc");

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var pagination = okResult.Value.Should().BeOfType<PaginationBase<PlayerSearchDto>>().Subject;
        
        pagination.Data.Should().HaveCount(2);
        pagination.Data.Should().Contain(p => p.Name == "Player1");
        pagination.Data.Should().Contain(p => p.Name == "Player2");
    }

    [Fact]
    public async Task SearchPlayers_ShouldRespectPrivacyFilter_WhenUserNotOwner()
    {
        // Arrange
        var players = new List<Player>
        {
            new() { LocalContentId = 1, Name = "PublicPlayer", CurrentWorldId = 65, IsPrivate = false, AccountId = 123 },
            new() { LocalContentId = 2, Name = "PrivatePlayer", CurrentWorldId = 66, IsPrivate = true, AccountId = 456 }
        };

        _context.Players.AddRange(players);
        await _context.SaveChangesAsync();

        // Setup HttpContext without GameAccountId (anonymous user)
        _controller.ControllerContext.HttpContext.Items.Clear();

        // Act
        var result = await _controller.SearchPlayers();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var pagination = okResult.Value.Should().BeOfType<PaginationBase<PlayerSearchDto>>().Subject;
        
        pagination.Data.Should().HaveCount(1);
        pagination.Data.First().Name.Should().Be("PublicPlayer");
    }

    [Fact]
    public async Task SearchPlayers_ShouldShowPrivatePlayer_WhenUserIsOwner()
    {
        // Arrange
        var players = new List<Player>
        {
            new() { LocalContentId = 1, Name = "PublicPlayer", CurrentWorldId = 65, IsPrivate = false, AccountId = 123 },
            new() { LocalContentId = 2, Name = "PrivatePlayer", CurrentWorldId = 66, IsPrivate = true, AccountId = 456 }
        };

        _context.Players.AddRange(players);
        await _context.SaveChangesAsync();

        // Setup HttpContext with GameAccountId matching the private player's owner
        _controller.ControllerContext.HttpContext.Items["GameAccountId"] = 456;

        // Act
        var result = await _controller.SearchPlayers();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var pagination = okResult.Value.Should().BeOfType<PaginationBase<PlayerSearchDto>>().Subject;
        
        pagination.Data.Should().HaveCount(2);
        pagination.Data.Should().Contain(p => p.Name == "PublicPlayer");
        pagination.Data.Should().Contain(p => p.Name == "PrivatePlayer");
    }

    [Fact]
    public async Task SearchPlayers_ShouldImplementCursorPagination()
    {
        // Arrange
        var players = new List<Player>();
        for (int i = 1; i <= 30; i++)
        {
            players.Add(new Player 
            { 
                LocalContentId = i, 
                Name = $"Player{i}", 
                CurrentWorldId = 65, 
                IsPrivate = false 
            });
        }

        _context.Players.AddRange(players);
        await _context.SaveChangesAsync();

        // Act - First page
        var firstPageResult = await _controller.SearchPlayers(Cursor: 0);

        // Assert - First page should have 25 items (PageSize)
        var okResult = firstPageResult.Result.Should().BeOfType<OkObjectResult>().Subject;
        var pagination = okResult.Value.Should().BeOfType<PaginationBase<PlayerSearchDto>>().Subject;
        
        pagination.Data.Should().HaveCount(25);
        pagination.LastCursor.Should().Be(25);
        pagination.NextCount.Should().Be(5); // Remaining 5 items

        // Act - Second page using cursor (cursor should be inclusive, so we need to use LastCursor + 1)
        var secondPageResult = await _controller.SearchPlayers(Cursor: pagination.LastCursor + 1);

        // Assert - Second page should have remaining 5 items
        var okResult2 = secondPageResult.Result.Should().BeOfType<OkObjectResult>().Subject;
        var pagination2 = okResult2.Value.Should().BeOfType<PaginationBase<PlayerSearchDto>>().Subject;
        
        pagination2.Data.Should().HaveCount(5);
        pagination2.NextCount.Should().Be(0); // No more items
    }

    [Fact]
    public async Task SearchPlayers_ShouldOrderByLocalContentId()
    {
        // Arrange
        var players = new List<Player>
        {
            new() { LocalContentId = 3, Name = "Player3", CurrentWorldId = 65, IsPrivate = false },
            new() { LocalContentId = 1, Name = "Player1", CurrentWorldId = 66, IsPrivate = false },
            new() { LocalContentId = 2, Name = "Player2", CurrentWorldId = 67, IsPrivate = false }
        };

        _context.Players.AddRange(players);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.SearchPlayers();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var pagination = okResult.Value.Should().BeOfType<PaginationBase<PlayerSearchDto>>().Subject;
        
        pagination.Data.Should().HaveCount(3);
        pagination.Data[0].LocalContentId.Should().Be(1);
        pagination.Data[1].LocalContentId.Should().Be(2);
        pagination.Data[2].LocalContentId.Should().Be(3);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task SearchPlayers_ShouldHandleEmptyNameFilter(string emptyName)
    {
        // Arrange
        var players = new List<Player>
        {
            new() { LocalContentId = 1, Name = "Player1", CurrentWorldId = 65, IsPrivate = false },
            new() { LocalContentId = 2, Name = "Player2", CurrentWorldId = 66, IsPrivate = false }
        };

        _context.Players.AddRange(players);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.SearchPlayers(Name: emptyName);

        // Assert - Should return all players when name filter is empty
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var pagination = okResult.Value.Should().BeOfType<PaginationBase<PlayerSearchDto>>().Subject;
        
        pagination.Data.Should().HaveCount(2);
    }

    [Fact]
    public async Task SearchPlayers_ShouldFilterByWhitespaceString()
    {
        // Arrange
        var players = new List<Player>
        {
            new() { LocalContentId = 1, Name = "Player1", CurrentWorldId = 65, IsPrivate = false },
            new() { LocalContentId = 2, Name = "   ", CurrentWorldId = 66, IsPrivate = false }, // Player with whitespace name
            new() { LocalContentId = 3, Name = "Player3", CurrentWorldId = 67, IsPrivate = false }
        };

        _context.Players.AddRange(players);
        await _context.SaveChangesAsync();

        // Act - Search for whitespace string
        var result = await _controller.SearchPlayers(Name: "   ");

        // Assert - Should find the player with whitespace name
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var pagination = okResult.Value.Should().BeOfType<PaginationBase<PlayerSearchDto>>().Subject;
        
        pagination.Data.Should().HaveCount(1);
        pagination.Data.First().Name.Should().Be("   ");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("invalid,world,ids")]
    [InlineData("abc,def,ghi")]
    public async Task SearchPlayers_ShouldHandleInvalidWorldIdsFilter(string invalidWorldIds)
    {
        // Arrange
        var players = new List<Player>
        {
            new() { LocalContentId = 1, Name = "Player1", CurrentWorldId = 65, IsPrivate = false },
            new() { LocalContentId = 2, Name = "Player2", CurrentWorldId = 66, IsPrivate = false }
        };

        _context.Players.AddRange(players);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.SearchPlayers(F_WorldIds: invalidWorldIds);

        // Assert - Should return all players when world filter is invalid/empty
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var pagination = okResult.Value.Should().BeOfType<PaginationBase<PlayerSearchDto>>().Subject;
        
        pagination.Data.Should().HaveCount(2);
    }

    [Fact]
    public async Task SearchPlayers_ShouldReturnEmptyResult_WhenNoPlayersMatch()
    {
        // Arrange
        var players = new List<Player>
        {
            new() { LocalContentId = 1, Name = "Player1", CurrentWorldId = 65, IsPrivate = false }
        };

        _context.Players.AddRange(players);
        await _context.SaveChangesAsync();

        // Act - Search for non-existent player
        var result = await _controller.SearchPlayers(Name: "NonExistentPlayer");

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var pagination = okResult.Value.Should().BeOfType<PaginationBase<PlayerSearchDto>>().Subject;
        
        pagination.Data.Should().BeEmpty();
        pagination.LastCursor.Should().Be(0);
        pagination.NextCount.Should().Be(0);
    }

    [Fact]
    public async Task SearchPlayers_ShouldMapPlayerDataCorrectly()
    {
        // Arrange
        var player = new Player
        {
            LocalContentId = 123456789,
            Name = "TestPlayer",
            CurrentWorldId = 65,
            AccountId = 1001,
            AvatarLink = "https://example.com/avatar.jpg",
            IsPrivate = false
        };

        _context.Players.Add(player);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.SearchPlayers();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var pagination = okResult.Value.Should().BeOfType<PaginationBase<PlayerSearchDto>>().Subject;
        
        pagination.Data.Should().HaveCount(1);
        var playerDto = pagination.Data.First();
        
        playerDto.LocalContentId.Should().Be(123456789);
        playerDto.Name.Should().Be("TestPlayer");
        playerDto.WorldId.Should().Be(65);
        playerDto.AccountId.Should().Be(1001);
        playerDto.AvatarLink.Should().Be("https://example.com/avatar.jpg");
    }
}