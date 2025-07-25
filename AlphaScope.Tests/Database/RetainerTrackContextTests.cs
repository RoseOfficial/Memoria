using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using AlphaScope.Database;
using TestUtilities;

namespace AlphaScope.Tests.Database;

public class RetainerTrackContextTests : IDisposable
{
    private readonly RetainerTrackContext _context;

    public RetainerTrackContextTests()
    {
        var options = new DbContextOptionsBuilder<RetainerTrackContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new RetainerTrackContext(options);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public void Constructor_ShouldInitializeSuccessfully()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<RetainerTrackContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Act
        var act = () => new RetainerTrackContext(options);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void DbSets_ShouldBeInitialized()
    {
        // Assert
        _context.Players.Should().NotBeNull();
        _context.Retainers.Should().NotBeNull();
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
            CurrentJobId = 19, // Paladin
            CurrentJobLevel = 90
        };

        // Act - Create
        _context.Players.Add(player);
        await _context.SaveChangesAsync();

        // Assert - Read
        var savedPlayer = await _context.Players.FindAsync(player.LocalContentId);
        savedPlayer.Should().NotBeNull();
        savedPlayer!.Name.Should().Be("Test Player");
        savedPlayer.AccountId.Should().Be(987654321);
        savedPlayer.CurrentJobId.Should().Be(19);
        savedPlayer.CurrentJobLevel.Should().Be(90);

        // Act - Update
        savedPlayer.Name = "Updated Player";
        savedPlayer.CurrentJobLevel = 91;
        await _context.SaveChangesAsync();

        // Assert - Updated
        var updatedPlayer = await _context.Players.FindAsync(player.LocalContentId);
        updatedPlayer!.Name.Should().Be("Updated Player");
        updatedPlayer.CurrentJobLevel.Should().Be(91);

        // Act - Delete
        _context.Players.Remove(updatedPlayer);
        await _context.SaveChangesAsync();

        // Assert - Deleted
        var deletedPlayer = await _context.Players.FindAsync(player.LocalContentId);
        deletedPlayer.Should().BeNull();
    }

    [Fact]
    public async Task Retainers_ShouldSupportBasicCrudOperations()
    {
        // Arrange
        var retainer = new Retainer
        {
            LocalContentId = 555666777,
            Name = "Test Retainer",
            WorldId = 65, // Malboro
            OwnerLocalContentId = 123456789
        };

        // Act - Create
        _context.Retainers.Add(retainer);
        await _context.SaveChangesAsync();

        // Assert - Read
        var savedRetainer = await _context.Retainers.FindAsync(retainer.LocalContentId);
        savedRetainer.Should().NotBeNull();
        savedRetainer!.Name.Should().Be("Test Retainer");
        savedRetainer.WorldId.Should().Be(65);
        savedRetainer.OwnerLocalContentId.Should().Be(123456789);

        // Act - Update
        savedRetainer.Name = "Updated Retainer";
        savedRetainer.WorldId = 66; // Hyperion
        await _context.SaveChangesAsync();

        // Assert - Updated
        var updatedRetainer = await _context.Retainers.FindAsync(retainer.LocalContentId);
        updatedRetainer!.Name.Should().Be("Updated Retainer");
        updatedRetainer.WorldId.Should().Be(66);

        // Act - Delete
        _context.Retainers.Remove(updatedRetainer);
        await _context.SaveChangesAsync();

        // Assert - Deleted
        var deletedRetainer = await _context.Retainers.FindAsync(retainer.LocalContentId);
        deletedRetainer.Should().BeNull();
    }

    [Fact]
    public async Task Players_ShouldSupportNullableProperties()
    {
        // Arrange
        var player = new Player
        {
            LocalContentId = 111222333,
            Name = "Minimal Player",
            AccountId = null,
            CurrentJobId = null,
            CurrentJobLevel = null
        };

        // Act
        _context.Players.Add(player);
        await _context.SaveChangesAsync();

        // Assert
        var savedPlayer = await _context.Players.FindAsync(player.LocalContentId);
        savedPlayer.Should().NotBeNull();
        savedPlayer!.Name.Should().Be("Minimal Player");
        savedPlayer.AccountId.Should().BeNull();
        savedPlayer.CurrentJobId.Should().BeNull();
        savedPlayer.CurrentJobLevel.Should().BeNull();
    }

    [Fact]
    public async Task Retainers_ShouldSupportNullableOwner()
    {
        // Arrange
        var retainer = new Retainer
        {
            LocalContentId = 444555666,
            Name = "Orphaned Retainer",
            WorldId = 65,
            OwnerLocalContentId = null
        };

        // Act
        _context.Retainers.Add(retainer);
        await _context.SaveChangesAsync();

        // Assert
        var savedRetainer = await _context.Retainers.FindAsync(retainer.LocalContentId);
        savedRetainer.Should().NotBeNull();
        savedRetainer!.Name.Should().Be("Orphaned Retainer");
        savedRetainer.WorldId.Should().Be(65);
        savedRetainer.OwnerLocalContentId.Should().BeNull();
    }

    [Fact]
    public async Task PlayerRetainerRelationship_ShouldWorkCorrectly()
    {
        // Arrange
        var player = new Player
        {
            LocalContentId = 111111111,
            Name = "Player Owner",
            AccountId = 222222222
        };

        var retainer1 = new Retainer
        {
            LocalContentId = 333333333,
            Name = "Retainer One",
            WorldId = 65,
            OwnerLocalContentId = player.LocalContentId
        };

        var retainer2 = new Retainer
        {
            LocalContentId = 444444444,
            Name = "Retainer Two",
            WorldId = 65,
            OwnerLocalContentId = player.LocalContentId
        };

        // Act
        _context.Players.Add(player);
        _context.Retainers.AddRange(retainer1, retainer2);
        await _context.SaveChangesAsync();

        // Assert - Query retainers by owner
        var ownedRetainers = await _context.Retainers
            .Where(r => r.OwnerLocalContentId == player.LocalContentId)
            .ToListAsync();

        ownedRetainers.Should().HaveCount(2);
        ownedRetainers.Should().Contain(r => r.Name == "Retainer One");
        ownedRetainers.Should().Contain(r => r.Name == "Retainer Two");

        // Assert - Query player by retainer
        var ownerExists = await _context.Players
            .AnyAsync(p => p.LocalContentId == retainer1.OwnerLocalContentId);
        ownerExists.Should().BeTrue();
    }

    [Fact]
    public async Task BulkOperations_ShouldHandleMultipleEntities()
    {
        // Arrange
        var players = new List<Player>();
        var retainers = new List<Retainer>();

        for (int i = 1; i <= 10; i++)
        {
            players.Add(new Player
            {
                LocalContentId = (ulong)(1000 + i),
                Name = $"Player {i}",
                AccountId = (ulong)(2000 + i)
            });

            retainers.Add(new Retainer
            {
                LocalContentId = (ulong)(3000 + i),
                Name = $"Retainer {i}",
                WorldId = 65,
                OwnerLocalContentId = (ulong)(1000 + i)
            });
        }

        // Act
        _context.Players.AddRange(players);
        _context.Retainers.AddRange(retainers);
        await _context.SaveChangesAsync();

        // Assert
        var playerCount = await _context.Players.CountAsync();
        var retainerCount = await _context.Retainers.CountAsync();

        playerCount.Should().Be(10);
        retainerCount.Should().Be(10);

        // Verify relationships
        var playersWithRetainers = await _context.Players
            .Where(p => _context.Retainers.Any(r => r.OwnerLocalContentId == p.LocalContentId))
            .CountAsync();

        playersWithRetainers.Should().Be(10);
    }

    [Fact]
    public async Task Queries_ShouldSupportComplexFiltering()
    {
        // Arrange
        var players = new[]
        {
            new Player { LocalContentId = 1, Name = "Tank Player", CurrentJobId = 19, CurrentJobLevel = 90 }, // Paladin
            new Player { LocalContentId = 2, Name = "DPS Player", CurrentJobId = 20, CurrentJobLevel = 85 }, // Monk
            new Player { LocalContentId = 3, Name = "Healer Player", CurrentJobId = 24, CurrentJobLevel = 80 }, // White Mage
            new Player { LocalContentId = 4, Name = "Low Level", CurrentJobId = 1, CurrentJobLevel = 15 } // Gladiator
        };

        var retainers = new[]
        {
            new Retainer { LocalContentId = 101, Name = "Malboro Retainer", WorldId = 65, OwnerLocalContentId = 1 },
            new Retainer { LocalContentId = 102, Name = "Hyperion Retainer", WorldId = 66, OwnerLocalContentId = 2 },
            new Retainer { LocalContentId = 103, Name = "Another Malboro", WorldId = 65, OwnerLocalContentId = 3 },
            new Retainer { LocalContentId = 104, Name = "Orphaned", WorldId = 65, OwnerLocalContentId = null }
        };

        _context.Players.AddRange(players);
        _context.Retainers.AddRange(retainers);
        await _context.SaveChangesAsync();

        // Act & Assert - High level players
        var highLevelPlayers = await _context.Players
            .Where(p => p.CurrentJobLevel >= 80)
            .ToListAsync();
        highLevelPlayers.Should().HaveCount(3);

        // Act & Assert - Retainers on specific world
        var malboroRetainers = await _context.Retainers
            .Where(r => r.WorldId == 65)
            .ToListAsync();
        malboroRetainers.Should().HaveCount(3);

        // Act & Assert - Retainers with owners
        var ownedRetainers = await _context.Retainers
            .Where(r => r.OwnerLocalContentId != null)
            .ToListAsync();
        ownedRetainers.Should().HaveCount(3);

        // Act & Assert - Players with retainers
        var playersWithRetainers = await _context.Players
            .Where(p => _context.Retainers.Any(r => r.OwnerLocalContentId == p.LocalContentId))
            .ToListAsync();
        playersWithRetainers.Should().HaveCount(3);
    }

    [Fact]
    public async Task ConcurrentAccess_ShouldHandleMultipleOperations()
    {
        // Arrange
        var tasks = new List<Task>();

        // Act - Simulate concurrent writes
        for (int i = 0; i < 5; i++)
        {
            int index = i;
            tasks.Add(Task.Run(async () =>
            {
                using var context = new RetainerTrackContext(
                    new DbContextOptionsBuilder<RetainerTrackContext>()
                        .UseInMemoryDatabase(databaseName: "ConcurrentTest")
                        .Options);

                var player = new Player
                {
                    LocalContentId = (ulong)(1000 + index),
                    Name = $"Concurrent Player {index}",
                    AccountId = (ulong)(2000 + index)
                };

                context.Players.Add(player);
                await context.SaveChangesAsync();
            }));
        }

        await Task.WhenAll(tasks);

        // Assert - All players should be created
        using var verifyContext = new RetainerTrackContext(
            new DbContextOptionsBuilder<RetainerTrackContext>()
                .UseInMemoryDatabase(databaseName: "ConcurrentTest")
                .Options);

        var playerCount = await verifyContext.Players.CountAsync();
        playerCount.Should().Be(5);
    }

    [Fact]
    public async Task DatabaseState_ShouldPersistAcrossOperations()
    {
        // Arrange & Act - Add data
        var player = new Player
        {
            LocalContentId = 999888777,
            Name = "Persistent Player",
            AccountId = 111222333
        };

        _context.Players.Add(player);
        await _context.SaveChangesAsync();

        // Clear change tracker to simulate fresh query
        _context.ChangeTracker.Clear();

        // Assert - Data should still exist
        var retrievedPlayer = await _context.Players.FindAsync(999888777UL);
        retrievedPlayer.Should().NotBeNull();
        retrievedPlayer!.Name.Should().Be("Persistent Player");
        retrievedPlayer.AccountId.Should().Be(111222333);
    }
}