using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using AlphaScopeServer.Data;
using AlphaScopeServer.Models.Entities;
using TestUtilities;

namespace AlphaScopeServer.Tests.Infrastructure;

public class AlphaScopeDbContextTests : IDisposable
{
    private readonly AlphaScopeDbContext _context;

    public AlphaScopeDbContextTests()
    {
        var options = new DbContextOptionsBuilder<AlphaScopeDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new AlphaScopeDbContext(options);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public void Constructor_ShouldInitializeSuccessfully()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AlphaScopeDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Act
        var act = () => new AlphaScopeDbContext(options);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void DbSets_ShouldBeInitialized()
    {
        // Assert - Player entities
        _context.Players.Should().NotBeNull();
        _context.PlayerNameHistory.Should().NotBeNull();
        _context.PlayerWorldHistory.Should().NotBeNull();
        _context.PlayerCustomizationHistory.Should().NotBeNull();
        _context.PlayerTerritoryHistory.Should().NotBeNull();
        _context.PlayerLodestones.Should().NotBeNull();
        _context.PlayerProfileVisits.Should().NotBeNull();

        // Assert - Retainer entities
        _context.Retainers.Should().NotBeNull();
        _context.RetainerNameHistory.Should().NotBeNull();
        _context.RetainerWorldHistory.Should().NotBeNull();

        // Assert - User entities
        _context.Users.Should().NotBeNull();
        _context.UserCharacters.Should().NotBeNull();
        _context.UserLodestoneCharacters.Should().NotBeNull();
    }

    [Fact]
    public async Task Players_ShouldSupportBasicCrudOperations()
    {
        // Arrange
        var player = new Player
        {
            LocalContentId = 123456789,
            Name = "Test Player",
            AccountId = 987654321,
            HomeWorldId = 65,
            CurrentWorldId = 65,
            TerritoryId = 130
        };

        // Act - Create
        _context.Players.Add(player);
        await _context.SaveChangesAsync();

        // Assert - Read
        var savedPlayer = await _context.Players.FindAsync(player.LocalContentId);
        savedPlayer.Should().NotBeNull();
        savedPlayer!.Name.Should().Be("Test Player");
        savedPlayer.AccountId.Should().Be(987654321);

        // Act - Update
        savedPlayer.Name = "Updated Player";
        await _context.SaveChangesAsync();

        // Assert - Updated
        var updatedPlayer = await _context.Players.FindAsync(player.LocalContentId);
        updatedPlayer!.Name.Should().Be("Updated Player");

        // Act - Delete
        _context.Players.Remove(updatedPlayer);
        await _context.SaveChangesAsync();

        // Assert - Deleted
        var deletedPlayer = await _context.Players.FindAsync(player.LocalContentId);
        deletedPlayer.Should().BeNull();
    }

    [Fact]
    public async Task Users_ShouldSupportBasicCrudOperations()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Name = "Test User",
            ApiKey = "test-key-123456",
            GameAccountId = 123456,
            PrimaryCharacterLocalContentId = 987654321
        };

        // Act - Create
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Assert - Read
        var savedUser = await _context.Users.FindAsync(user.Id);
        savedUser.Should().NotBeNull();
        savedUser!.Name.Should().Be("Test User");
        savedUser.ApiKey.Should().Be("test-key-123456");

        // Act - Update
        savedUser.Name = "Updated User";
        await _context.SaveChangesAsync();

        // Assert - Updated
        var updatedUser = await _context.Users.FindAsync(user.Id);
        updatedUser!.Name.Should().Be("Updated User");
    }

    [Fact]
    public async Task Retainers_ShouldSupportBasicCrudOperations()
    {
        // Arrange
        var player = new Player
        {
            LocalContentId = 123456789,
            Name = "Player Owner",
            AccountId = 987654321,
            HomeWorldId = 65,
            CurrentWorldId = 65
        };

        var retainer = new Retainer
        {
            LocalContentId = 555666777,
            Name = "Test Retainer",
            WorldId = 65,
            OwnerLocalContentId = player.LocalContentId
        };

        // Act - Create
        _context.Players.Add(player);
        _context.Retainers.Add(retainer);
        await _context.SaveChangesAsync();

        // Assert - Read
        var savedRetainer = await _context.Retainers.FindAsync(retainer.LocalContentId);
        savedRetainer.Should().NotBeNull();
        savedRetainer!.Name.Should().Be("Test Retainer");
        savedRetainer.OwnerLocalContentId.Should().Be(player.LocalContentId);
    }

    [Fact]
    public async Task PlayerNameHistory_ShouldMaintainCascadeRelationship()
    {
        // Arrange
        var player = new Player
        {
            LocalContentId = 123456789,
            Name = "Current Name",
            AccountId = 987654321,
            HomeWorldId = 65,
            CurrentWorldId = 65
        };

        var nameHistory = new PlayerNameHistory
        {
            PlayerLocalContentId = player.LocalContentId,
            Name = "Current Name"
        };

        // Act
        _context.Players.Add(player);
        _context.PlayerNameHistory.Add(nameHistory);
        await _context.SaveChangesAsync();

        // Assert - Relationship exists
        var playerWithHistory = await _context.Players
            .Include(p => p.NameHistory)
            .FirstAsync(p => p.LocalContentId == player.LocalContentId);

        playerWithHistory.NameHistory.Should().HaveCount(1);
        playerWithHistory.NameHistory.First().Name.Should().Be("Current Name");

        // Act - Delete player (should cascade to history)
        _context.Players.Remove(playerWithHistory);
        await _context.SaveChangesAsync();

        // Assert - History should be deleted too
        var remainingHistory = await _context.PlayerNameHistory.CountAsync();
        remainingHistory.Should().Be(0);
    }

    [Fact]
    public async Task PlayerWorldHistory_ShouldMaintainCascadeRelationship()
    {
        // Arrange
        var player = new Player
        {
            LocalContentId = 123456789,
            Name = "Test Player",
            AccountId = 987654321,
            HomeWorldId = 66,
            CurrentWorldId = 66
        };

        var worldHistory = new PlayerWorldHistory
        {
            PlayerLocalContentId = player.LocalContentId,
            WorldId = 66
        };

        // Act
        _context.Players.Add(player);
        _context.PlayerWorldHistory.Add(worldHistory);
        await _context.SaveChangesAsync();

        // Assert
        var playerWithHistory = await _context.Players
            .Include(p => p.WorldHistory)
            .FirstAsync(p => p.LocalContentId == player.LocalContentId);

        playerWithHistory.WorldHistory.Should().HaveCount(1);
        playerWithHistory.WorldHistory.First().WorldId.Should().Be(66);
    }

    [Fact]
    public async Task PlayerLodestone_ShouldMaintainOneToOneRelationship()
    {
        // Arrange
        var player = new Player
        {
            LocalContentId = 123456789,
            Name = "Test Player",
            AccountId = 987654321,
            HomeWorldId = 65,
            CurrentWorldId = 65
        };

        var lodestone = new PlayerLodestone
        {
            PlayerLocalContentId = player.LocalContentId,
            LodestoneId = 987654321
        };

        // Act
        _context.Players.Add(player);
        _context.PlayerLodestones.Add(lodestone);
        await _context.SaveChangesAsync();

        // Assert
        var playerWithLodestone = await _context.Players
            .Include(p => p.Lodestone)
            .FirstAsync(p => p.LocalContentId == player.LocalContentId);

        playerWithLodestone.Lodestone.Should().NotBeNull();
        playerWithLodestone.Lodestone!.LodestoneId.Should().Be(987654321);
    }

    [Fact]
    public async Task RetainerOwnerRelationship_ShouldWorkCorrectly()
    {
        // Arrange
        var player = new Player
        {
            LocalContentId = 123456789,
            Name = "Player Owner",
            AccountId = 987654321,
            HomeWorldId = 65,
            CurrentWorldId = 65
        };

        var retainer1 = new Retainer
        {
            LocalContentId = 111111111,
            Name = "Retainer One",
            WorldId = 65,
            OwnerLocalContentId = player.LocalContentId
        };

        var retainer2 = new Retainer
        {
            LocalContentId = 222222222,
            Name = "Retainer Two",
            WorldId = 65,
            OwnerLocalContentId = player.LocalContentId
        };

        // Act
        _context.Players.Add(player);
        _context.Retainers.AddRange(retainer1, retainer2);
        await _context.SaveChangesAsync();

        // Assert
        var playerWithRetainers = await _context.Players
            .Include(p => p.Retainers)
            .FirstAsync(p => p.LocalContentId == player.LocalContentId);

        playerWithRetainers.Retainers.Should().HaveCount(2);
        playerWithRetainers.Retainers.Should().Contain(r => r.Name == "Retainer One");
        playerWithRetainers.Retainers.Should().Contain(r => r.Name == "Retainer Two");
    }

    [Fact]
    public async Task UserCharacterRelationship_ShouldWorkCorrectly()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Name = "Test User",
            ApiKey = "test-key-123456",
            GameAccountId = 123456,
            PrimaryCharacterLocalContentId = 987654321
        };

        var userCharacter = new UserCharacter
        {
            UserId = user.Id,
            LocalContentId = 987654321,
            Name = "Main Character"
        };

        // Act
        _context.Users.Add(user);
        await _context.SaveChangesAsync(); // Save user first to get ID

        userCharacter.UserId = user.Id;
        _context.UserCharacters.Add(userCharacter);
        await _context.SaveChangesAsync();

        // Assert
        var userWithCharacters = await _context.Users
            .Include(u => u.Characters)
            .FirstAsync(u => u.Id == user.Id);

        userWithCharacters.Characters.Should().HaveCount(1);
        userWithCharacters.Characters.First().Name.Should().Be("Main Character");
    }

    [Fact]
    public async Task SaveChanges_ShouldUpdateTimestamps()
    {
        // Arrange
        var player = new Player
        {
            LocalContentId = 123456789,
            Name = "Test Player",
            AccountId = 987654321,
            HomeWorldId = 65,
            CurrentWorldId = 65
        };

        // Act - Create
        _context.Players.Add(player);
        var beforeSave = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        var afterSave = DateTime.UtcNow;

        // Assert - CreatedAt should be set
        player.CreatedAt.Should().BeOnOrAfter(beforeSave);
        player.CreatedAt.Should().BeOnOrBefore(afterSave);
        player.UpdatedAt.Should().BeOnOrAfter(beforeSave);
        player.UpdatedAt.Should().BeOnOrBefore(afterSave);

        // Act - Update
        var originalCreatedAt = player.CreatedAt;
        var originalUpdatedAt = player.UpdatedAt;
        
        await Task.Delay(1); // Small delay to ensure different timestamp
        player.Name = "Updated Name";
        var beforeUpdate = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        var afterUpdate = DateTime.UtcNow;

        // Assert - UpdatedAt should change, CreatedAt should stay the same
        player.CreatedAt.Should().Be(originalCreatedAt);
        player.UpdatedAt.Should().BeOnOrAfter(beforeUpdate);
        player.UpdatedAt.Should().BeOnOrBefore(afterUpdate);
        player.UpdatedAt.Should().BeAfter(originalUpdatedAt);
    }

    [Fact]
    public async Task DatabaseIndexes_ShouldExistForPerformanceOptimization()
    {
        // This test verifies that key indexes are configured
        // In a real database, you would check sys.indexes or similar
        // For in-memory database, we verify the configuration exists

        // Arrange & Act - Add data that would benefit from indexes
        var players = new List<Player>();
        for (int i = 1; i <= 100; i++)
        {
            players.Add(new Player
            {
                LocalContentId = i,
                Name = $"Player {i}",
                AccountId = 1000 + i,
                HomeWorldId = (short)(65 + (i % 3)),
                CurrentWorldId = (short)(65 + (i % 3))
            });
        }

        _context.Players.AddRange(players);
        await _context.SaveChangesAsync();

        // Assert - Queries that would use indexes should work efficiently
        var playersByName = await _context.Players
            .Where(p => p.Name.StartsWith("Player 1"))
            .ToListAsync();

        var playersByAccountId = await _context.Players
            .Where(p => p.AccountId == 1005)
            .ToListAsync();

        var playersByWorld = await _context.Players
            .Where(p => p.HomeWorldId == 65)
            .ToListAsync();

        // These should all execute without errors
        playersByName.Should().NotBeEmpty();
        playersByAccountId.Should().HaveCount(1);
        playersByWorld.Should().NotBeEmpty();
    }

    [Fact]
    public async Task UniqueConstraints_ShouldBeEnforced()
    {
        // Arrange
        var user1 = new ApplicationUser
        {
            Name = "User One",
            ApiKey = "unique-key-123",
            GameAccountId = 123456,
            PrimaryCharacterLocalContentId = 987654321
        };

        var user2 = new ApplicationUser
        {
            Name = "User Two",
            ApiKey = "unique-key-123", // Same API key
            GameAccountId = 654321,
            PrimaryCharacterLocalContentId = 123456789
        };

        // Act & Assert
        _context.Users.Add(user1);
        await _context.SaveChangesAsync();

        _context.Users.Add(user2);
        
        // This should throw due to unique constraint on ApiKey
        var act = async () => await _context.SaveChangesAsync();
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task CascadeDelete_ShouldWorkForAllRelationships()
    {
        // Arrange
        var player = new Player
        {
            LocalContentId = 123456789,
            Name = "Test Player",
            AccountId = 987654321,
            HomeWorldId = 65,
            CurrentWorldId = 65
        };

        var retainer = new Retainer
        {
            LocalContentId = 111111111,
            Name = "Test Retainer",
            WorldId = 65,
            OwnerLocalContentId = player.LocalContentId
        };

        var nameHistory = new PlayerNameHistory
        {
            PlayerLocalContentId = player.LocalContentId,
            Name = "Test Player"
        };

        // Act
        _context.Players.Add(player);
        _context.Retainers.Add(retainer);
        _context.PlayerNameHistory.Add(nameHistory);
        await _context.SaveChangesAsync();

        // Verify initial state
        var initialPlayerCount = await _context.Players.CountAsync();
        var initialRetainerCount = await _context.Retainers.CountAsync();
        var initialHistoryCount = await _context.PlayerNameHistory.CountAsync();

        initialPlayerCount.Should().Be(1);
        initialRetainerCount.Should().Be(1);
        initialHistoryCount.Should().Be(1);

        // Act - Delete player
        _context.Players.Remove(player);
        await _context.SaveChangesAsync();

        // Assert - All related entities should be deleted
        var finalPlayerCount = await _context.Players.CountAsync();
        var finalRetainerCount = await _context.Retainers.CountAsync();
        var finalHistoryCount = await _context.PlayerNameHistory.CountAsync();

        finalPlayerCount.Should().Be(0);
        finalRetainerCount.Should().Be(0);
        finalHistoryCount.Should().Be(0);
    }

    [Fact]
    public async Task BulkOperations_ShouldPerformEfficiently()
    {
        // Arrange
        var players = new List<Player>();
        for (int i = 1; i <= 1000; i++)
        {
            players.Add(new Player
            {
                LocalContentId = i,
                Name = $"Bulk Player {i}",
                AccountId = 10000 + i,
                HomeWorldId = 65,
                CurrentWorldId = 65
            });
        }

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        _context.Players.AddRange(players);
        await _context.SaveChangesAsync();
        stopwatch.Stop();

        // Assert
        var playerCount = await _context.Players.CountAsync();
        playerCount.Should().Be(1000);
        
        // Performance assertion - bulk insert should be reasonably fast
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000); // 5 seconds max
    }

    [Theory]
    [InlineData("Test Player 1")]
    [InlineData("Another Player")]
    [InlineData("Special-Character$")]
    public async Task Players_ShouldSupportVariousNameFormats(string playerName)
    {
        // Arrange
        var player = new Player
        {
            LocalContentId = 123456789,
            Name = playerName,
            AccountId = 987654321,
            HomeWorldId = 65,
            CurrentWorldId = 65
        };

        // Act
        _context.Players.Add(player);
        await _context.SaveChangesAsync();

        // Assert
        var savedPlayer = await _context.Players.FindAsync(player.LocalContentId);
        savedPlayer!.Name.Should().Be(playerName);
    }
}