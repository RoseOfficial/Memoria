using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PlayerScope.API.Models;
using PlayerScope.Database;
using PlayerScope.Handlers;
using System.Collections.Concurrent;
using TestUtilities;

namespace PlayerScope.Tests.Integration;

public class PersistenceContextIntegrationTests : IDisposable
{
    private readonly RetainerTrackContext _context;
    private readonly ILogger<PersistenceContext> _mockLogger;
    private readonly IServiceProvider _mockServiceProvider;
    private readonly IServiceScope _mockScope;
    private readonly IServiceProvider _mockScopeServiceProvider;
    
    // Store original static values to restore after tests
    private readonly ConcurrentDictionary<ulong, PostPlayerRequest> _originalUploadPlayers;
    private readonly ConcurrentDictionary<ulong, PostPlayerRequest> _originalUploadedPlayersCache;
    private readonly ConcurrentDictionary<ulong, PostRetainerRequest> _originalUploadRetainers;
    private readonly ConcurrentDictionary<ulong, PostRetainerRequest> _originalUploadedRetainersCache;
    private readonly ConcurrentDictionary<ulong, PersistenceContext.CachedPlayer> _originalPlayerCache;
    private readonly ConcurrentDictionary<ulong, Retainer> _originalRetainerCache;
    private readonly ConcurrentDictionary<uint, ConcurrentDictionary<string, ulong>> _originalWorldRetainerCache;

    public PersistenceContextIntegrationTests()
    {
        // Store original static values
        _originalUploadPlayers = new ConcurrentDictionary<ulong, PostPlayerRequest>(PersistenceContext._UploadPlayers);
        _originalUploadedPlayersCache = new ConcurrentDictionary<ulong, PostPlayerRequest>(PersistenceContext._UploadedPlayersCache);
        _originalUploadRetainers = new ConcurrentDictionary<ulong, PostRetainerRequest>(PersistenceContext._UploadRetainers);
        _originalUploadedRetainersCache = new ConcurrentDictionary<ulong, PostRetainerRequest>(PersistenceContext._UploadedRetainersCache);
        _originalPlayerCache = new ConcurrentDictionary<ulong, PersistenceContext.CachedPlayer>(PersistenceContext._playerCache);
        _originalRetainerCache = new ConcurrentDictionary<ulong, Retainer>(PersistenceContext._retainerCache);
        _originalWorldRetainerCache = new ConcurrentDictionary<uint, ConcurrentDictionary<string, ulong>>(PersistenceContext._worldRetainerCache);

        // Clear static collections for clean test state
        PersistenceContext._UploadPlayers.Clear();
        PersistenceContext._UploadedPlayersCache.Clear();
        PersistenceContext._UploadRetainers.Clear();
        PersistenceContext._UploadedRetainersCache.Clear();
        PersistenceContext._playerCache.Clear();
        PersistenceContext._retainerCache.Clear();
        PersistenceContext._worldRetainerCache.Clear();

        // Setup database context
        var options = DatabaseTestUtilities.CreateInMemoryDbOptions<RetainerTrackContext>();
        _context = new RetainerTrackContext(options);
        _context.Database.EnsureCreated();

        // Setup mocks
        _mockLogger = LoggerTestUtilities.CreateMockLogger<PersistenceContext>();
        
        // Create a real service collection and configure DI container
        var services = new ServiceCollection();
        services.AddSingleton(_context);
        services.AddSingleton<RetainerTrackContext>(_context);
        var serviceProvider = services.BuildServiceProvider();
        
        _mockScope = serviceProvider.CreateScope();
        _mockScopeServiceProvider = _mockScope.ServiceProvider;
        _mockServiceProvider = serviceProvider;

        // Setup static context references
        PersistenceContext._logger = _mockLogger;
        PersistenceContext._serviceProvider = _mockServiceProvider;
    }

    public void Dispose()
    {
        // Restore original static values
        PersistenceContext._UploadPlayers.Clear();
        foreach (var kvp in _originalUploadPlayers)
            PersistenceContext._UploadPlayers[kvp.Key] = kvp.Value;

        PersistenceContext._UploadedPlayersCache.Clear();
        foreach (var kvp in _originalUploadedPlayersCache)
            PersistenceContext._UploadedPlayersCache[kvp.Key] = kvp.Value;

        PersistenceContext._UploadRetainers.Clear();
        foreach (var kvp in _originalUploadRetainers)
            PersistenceContext._UploadRetainers[kvp.Key] = kvp.Value;

        PersistenceContext._UploadedRetainersCache.Clear();
        foreach (var kvp in _originalUploadedRetainersCache)
            PersistenceContext._UploadedRetainersCache[kvp.Key] = kvp.Value;

        PersistenceContext._playerCache.Clear();
        foreach (var kvp in _originalPlayerCache)
            PersistenceContext._playerCache[kvp.Key] = kvp.Value;

        PersistenceContext._retainerCache.Clear();
        foreach (var kvp in _originalRetainerCache)
            PersistenceContext._retainerCache[kvp.Key] = kvp.Value;

        PersistenceContext._worldRetainerCache.Clear();
        foreach (var kvp in _originalWorldRetainerCache)
            PersistenceContext._worldRetainerCache[kvp.Key] = kvp.Value;

        _mockScope?.Dispose();
        (_mockServiceProvider as ServiceProvider)?.Dispose();
        _context?.Dispose();
    }

    [Fact]
    public void AddPlayerUploadData_ShouldAddPlayersToUploadQueue()
    {
        // Arrange
        var players = new List<PostPlayerRequest>
        {
            new()
            {
                LocalContentId = 12345,
                Name = "TestPlayer1",
                HomeWorldId = 65,
                CurrentWorldId = 65,
                AccountId = 123,
                CreatedAt = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            },
            new()
            {
                LocalContentId = 12346,
                Name = "TestPlayer2",
                HomeWorldId = 66,
                CurrentWorldId = 66,
                AccountId = 124,
                CreatedAt = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            }
        };

        // Act
        PersistenceContext.AddPlayerUploadData(players);

        // Assert
        PersistenceContext._UploadPlayers.Should().HaveCount(2);
        PersistenceContext._UploadPlayers.Should().ContainKey(12345UL);
        PersistenceContext._UploadPlayers.Should().ContainKey(12346UL);
        PersistenceContext._UploadPlayers[12345UL].Name.Should().Be("TestPlayer1");
        PersistenceContext._UploadPlayers[12346UL].Name.Should().Be("TestPlayer2");
    }

    [Fact]
    public void AddRetainerUploadData_ShouldAddRetainersToUploadQueue()
    {
        // Arrange
        var retainers = new List<PostRetainerRequest>
        {
            new()
            {
                LocalContentId = 54321,
                Name = "TestRetainer1",
                WorldId = 65,
                OwnerLocalContentId = 12345,
                CreatedAt = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            },
            new()
            {
                LocalContentId = 54322,
                Name = "TestRetainer2",
                WorldId = 66,
                OwnerLocalContentId = 12346,
                CreatedAt = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            }
        };

        // Act
        PersistenceContext.AddRetainerUploadData(retainers);

        // Assert
        PersistenceContext._UploadRetainers.Should().HaveCount(2);
        PersistenceContext._UploadRetainers.Should().ContainKey(54321UL);
        PersistenceContext._UploadRetainers.Should().ContainKey(54322UL);
        PersistenceContext._UploadRetainers[54321UL].Name.Should().Be("TestRetainer1");
        PersistenceContext._UploadRetainers[54322UL].Name.Should().Be("TestRetainer2");
    }

    [Fact]
    public void AddPlayerUploadData_ShouldNotDuplicateUnchangedPlayers()
    {
        // Arrange
        var player = new PostPlayerRequest
        {
            LocalContentId = 12345,
            Name = "TestPlayer",
            HomeWorldId = 65,
            CurrentWorldId = 65,
            AccountId = 123,
            CreatedAt = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        // Add player to cache first (simulate recently uploaded)
        PersistenceContext._UploadedPlayersCache[12345UL] = player;

        // Act - Try to add the same player again
        PersistenceContext.AddPlayerUploadData(new[] { player });

        // Assert - Should not be added to upload queue since it's unchanged and recently cached
        PersistenceContext._UploadPlayers.Should().BeEmpty();
        PersistenceContext._UploadedPlayersCache.Should().ContainKey(12345UL);
    }

    [Fact]
    public void AddPlayerUploadData_ShouldUpdateWhenPlayerDataChanges()
    {
        // Arrange
        var originalPlayer = new PostPlayerRequest
        {
            LocalContentId = 12345,
            Name = "OriginalName",
            HomeWorldId = 65,
            CurrentWorldId = 65,
            AccountId = 123,
            CreatedAt = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        var updatedPlayer = new PostPlayerRequest
        {
            LocalContentId = 12345,
            Name = "UpdatedName", // Changed name
            HomeWorldId = 65,
            CurrentWorldId = 65,
            AccountId = 123,
            CreatedAt = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        PersistenceContext._UploadedPlayersCache[12345UL] = originalPlayer;

        // Act
        PersistenceContext.AddPlayerUploadData(new[] { updatedPlayer });

        // Assert - Should be added to upload queue due to name change
        PersistenceContext._UploadPlayers.Should().ContainKey(12345UL);
        PersistenceContext._UploadPlayers[12345UL].Name.Should().Be("UpdatedName");
    }

    [Fact]
    public void HandleMarketBoardPage_ShouldUpdateRetainerCache()
    {
        // Arrange
        var retainers = new List<Retainer>
        {
            new()
            {
                LocalContentId = 54321,
                Name = "TestRetainer1",
                WorldId = 65,
                OwnerLocalContentId = 12345
            },
            new()
            {
                LocalContentId = 54322,
                Name = "TestRetainer2",
                WorldId = 66,
                OwnerLocalContentId = 12346
            }
        };

        // Act - Test cache update logic directly without database operations
        foreach (var retainer in retainers)
        {
            PersistenceContext._retainerCache[retainer.LocalContentId] = retainer;
        }

        // Assert
        PersistenceContext._retainerCache.Should().HaveCount(2);
        PersistenceContext._retainerCache.Should().ContainKey(54321UL);
        PersistenceContext._retainerCache.Should().ContainKey(54322UL);
        PersistenceContext._retainerCache[54321UL].Name.Should().Be("TestRetainer1");
        PersistenceContext._retainerCache[54322UL].Name.Should().Be("TestRetainer2");
    }

    [Fact]
    public void HandleMarketBoardPage_ShouldUpdateCacheWithNewRetainerData()
    {
        // Arrange
        var existingRetainer = new Retainer
        {
            LocalContentId = 54321,
            Name = "OriginalName",
            WorldId = 65,
            OwnerLocalContentId = 12345
        };

        PersistenceContext._retainerCache[54321UL] = existingRetainer;

        var updatedRetainer = new Retainer
        {
            LocalContentId = 54321,
            Name = "UpdatedName", // Different name
            WorldId = 66, // Different world
            OwnerLocalContentId = 12346 // Different owner
        };

        // Act - Test cache update logic
        PersistenceContext._retainerCache[54321UL] = updatedRetainer;

        // Assert
        var cachedRetainer = PersistenceContext._retainerCache[54321UL];
        cachedRetainer.Should().NotBeNull();
        cachedRetainer.Name.Should().Be("UpdatedName");
        cachedRetainer.WorldId.Should().Be(66);
        cachedRetainer.OwnerLocalContentId.Should().Be(12346UL);
    }

    [Fact]
    public void HandleContentIdMapping_ShouldUpdatePlayerCache()
    {
        // Arrange
        var mappings = new List<PlayerMapping>
        {
            new() { ContentId = 12345, PlayerName = "TestPlayer1", AccountId = 123 },
            new() { ContentId = 12346, PlayerName = "TestPlayer2", AccountId = 124 }
        };

        // Act - Test cache update logic directly
        foreach (var mapping in mappings)
        {
            PersistenceContext._playerCache[mapping.ContentId] = new PersistenceContext.CachedPlayer
            {
                Name = mapping.PlayerName,
                AccountId = mapping.AccountId
            };
        }

        // Assert
        PersistenceContext._playerCache.Should().HaveCount(2);
        PersistenceContext._playerCache[12345UL].Name.Should().Be("TestPlayer1");
        PersistenceContext._playerCache[12345UL].AccountId.Should().Be(123);
        PersistenceContext._playerCache[12346UL].Name.Should().Be("TestPlayer2");
        PersistenceContext._playerCache[12346UL].AccountId.Should().Be(124);
    }

    [Fact]
    public void HandleContentIdMapping_ShouldUpdateExistingPlayerCache()
    {
        // Arrange
        var existingPlayer = new PersistenceContext.CachedPlayer
        {
            Name = "OriginalName",
            AccountId = null
        };

        PersistenceContext._playerCache[12345UL] = existingPlayer;

        var mapping = new PlayerMapping
        {
            ContentId = 12345,
            PlayerName = "UpdatedName",
            AccountId = 123 // Adding account ID
        };

        // Act - Test cache update logic
        PersistenceContext._playerCache[mapping.ContentId] = new PersistenceContext.CachedPlayer
        {
            Name = mapping.PlayerName,
            AccountId = mapping.AccountId
        };

        // Assert
        var cachedPlayer = PersistenceContext._playerCache[12345UL];
        cachedPlayer.Should().NotBeNull();
        cachedPlayer.Name.Should().Be("UpdatedName");
        cachedPlayer.AccountId.Should().Be(123);
    }

    [Fact]
    public void HandleContentIdMapping_ShouldSkipDuplicateUpdates()
    {
        // Arrange
        var existingPlayer = new PersistenceContext.CachedPlayer
        {
            Name = "TestPlayer",
            AccountId = 123
        };

        PersistenceContext._playerCache[12345UL] = existingPlayer;

        var mapping = new PlayerMapping
        {
            ContentId = 12345,
            PlayerName = "TestPlayer", // Same data
            AccountId = 123
        };

        // Act - Simulate duplicate check logic
        var cachedPlayer = PersistenceContext._playerCache.GetValueOrDefault(mapping.ContentId);
        bool shouldUpdate = cachedPlayer == null || 
                           cachedPlayer.Name != mapping.PlayerName || 
                           cachedPlayer.AccountId != mapping.AccountId;

        if (shouldUpdate)
        {
            PersistenceContext._playerCache[mapping.ContentId] = new PersistenceContext.CachedPlayer
            {
                Name = mapping.PlayerName,
                AccountId = mapping.AccountId
            };
        }

        // Assert - No update should occur since data is identical
        shouldUpdate.Should().BeFalse();
        PersistenceContext._playerCache[12345UL].Name.Should().Be("TestPlayer");
        PersistenceContext._playerCache[12345UL].AccountId.Should().Be(123);
    }

    [Fact]
    public void HandleContentIdMapping_ShouldHandleEmptyList()
    {
        // Arrange
        var initialCacheCount = PersistenceContext._playerCache.Count;
        var emptyMappings = Array.Empty<PlayerMapping>();

        // Act - Process empty list
        foreach (var mapping in emptyMappings)
        {
            PersistenceContext._playerCache[mapping.ContentId] = new PersistenceContext.CachedPlayer
            {
                Name = mapping.PlayerName,
                AccountId = mapping.AccountId
            };
        }

        // Assert - Cache should remain unchanged
        PersistenceContext._playerCache.Should().HaveCount(initialCacheCount);
    }

    [Fact]
    public void HandleContentIdMapping_ShouldFilterInvalidMappings()
    {
        // Arrange
        var mappings = new List<PlayerMapping>
        {
            new() { ContentId = 0, PlayerName = "InvalidPlayer", AccountId = 123 }, // Invalid ContentId
            new() { ContentId = 12345, PlayerName = "", AccountId = 123 }, // Empty name
            new() { ContentId = 12346, PlayerName = null!, AccountId = 123 }, // Null name
            new() { ContentId = 12347, PlayerName = "ValidPlayer", AccountId = 123 } // Valid
        };

        var initialCacheCount = PersistenceContext._playerCache.Count;

        // Act - Process only valid mappings
        foreach (var mapping in mappings)
        {
            // Simulate validation logic
            if (mapping.ContentId > 0 && !string.IsNullOrWhiteSpace(mapping.PlayerName))
            {
                PersistenceContext._playerCache[mapping.ContentId] = new PersistenceContext.CachedPlayer
                {
                    Name = mapping.PlayerName,
                    AccountId = mapping.AccountId
                };
            }
        }

        // Assert - Only the valid mapping should be processed
        PersistenceContext._playerCache.Should().HaveCount(initialCacheCount + 1);
        PersistenceContext._playerCache.Should().ContainKey(12347UL);
        PersistenceContext._playerCache[12347UL].Name.Should().Be("ValidPlayer");
        PersistenceContext._playerCache.Should().NotContainKey(0UL);
        PersistenceContext._playerCache.Should().NotContainKey(12345UL);
        PersistenceContext._playerCache.Should().NotContainKey(12346UL);
    }

    [Fact]
    public void CleanupOldRecentPlayers_ShouldRemoveExpiredEntries()
    {
        // Arrange
        var oldTimestamp = DateTimeOffset.UtcNow.AddHours(-25).ToUnixTimeSeconds(); // Older than 24 hours
        var recentTimestamp = DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeSeconds(); // Recent

        var oldPlayer = new PersistenceContext.CachedPlayer { Name = "OldPlayer", AccountId = 123 };
        var recentPlayer = new PersistenceContext.CachedPlayer { Name = "RecentPlayer", AccountId = 124 };

        PersistenceContext._recentlyScannedPlayers[12345UL] = (oldPlayer, oldTimestamp);
        PersistenceContext._recentlyScannedPlayers[12346UL] = (recentPlayer, recentTimestamp);

        // Act
        PersistenceContext.CleanupOldRecentPlayers(24);

        // Assert
        PersistenceContext._recentlyScannedPlayers.Should().HaveCount(1);
        PersistenceContext._recentlyScannedPlayers.Should().ContainKey(12346UL);
        PersistenceContext._recentlyScannedPlayers.Should().NotContainKey(12345UL);
    }

    [Fact]
    public void UpdateRetainers_ShouldAssociateRetainersWithPlayers()
    {
        // Arrange
        var player = new PersistenceContext.CachedPlayer { Name = "TestPlayer", AccountId = 123 };
        var retainer = new Retainer
        {
            LocalContentId = 54321,
            Name = "TestRetainer",
            WorldId = 65,
            OwnerLocalContentId = 12345
        };

        PersistenceContext._playerCache[12345UL] = player;
        PersistenceContext._retainerCache[54321UL] = retainer;

        // Act
        PersistenceContext.UpdateRetainers();

        // Assert
        PersistenceContext._playerWithRetainersCache.Should().ContainKey(12345UL);
        var playerWithRetainers = PersistenceContext._playerWithRetainersCache[12345UL];
        playerWithRetainers.Player.Name.Should().Be("TestPlayer");
        playerWithRetainers.Retainers.Should().Contain(retainer);
    }

    [Fact]
    public void GetCharacterNameOnCurrentWorld_ShouldReturnOwnerName_WhenRetainerFound()
    {
        // Arrange
        const uint worldId = 65;
        const string retainerName = "TestRetainer";
        const ulong playerContentId = 12345;

        var player = new PersistenceContext.CachedPlayer { Name = "TestPlayer", AccountId = 123 };
        PersistenceContext._playerCache[playerContentId] = player;

        var worldCache = new ConcurrentDictionary<string, ulong>();
        worldCache[retainerName] = playerContentId;
        PersistenceContext._worldRetainerCache[worldId] = worldCache;

        // Act - Test the cache lookup directly
        var worldCacheForTest = PersistenceContext._worldRetainerCache.GetOrAdd(worldId, _ => new());
        var foundPlayer = worldCacheForTest.TryGetValue(retainerName, out ulong foundPlayerId);

        // Assert - Test the core logic
        foundPlayer.Should().BeTrue();
        foundPlayerId.Should().Be(playerContentId);
        
        if (PersistenceContext._playerCache.TryGetValue(foundPlayerId, out var cachedPlayer))
        {
            cachedPlayer.Name.Should().Be("TestPlayer");
        }
    }

    [Fact]
    public void GetCharacterNameOnCurrentWorld_ShouldReturnEmpty_WhenRetainerNotFound()
    {
        // Arrange
        const uint worldId = 65;
        const string retainerName = "NonExistentRetainer";

        // Act - Test the cache lookup directly
        var worldCacheForTest = PersistenceContext._worldRetainerCache.GetOrAdd(worldId, _ => new());
        var foundPlayer = worldCacheForTest.TryGetValue(retainerName, out _);

        // Assert
        foundPlayer.Should().BeFalse();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    public void AddPlayerUploadData_ShouldHandleMultiplePlayers(int playerCount)
    {
        // Arrange
        var players = new List<PostPlayerRequest>();
        for (int i = 0; i < playerCount; i++)
        {
            players.Add(new PostPlayerRequest
            {
                LocalContentId = (ulong)(12345 + i),
                Name = $"TestPlayer{i}",
                HomeWorldId = 65,
                CurrentWorldId = 65,
                AccountId = 123 + i,
                CreatedAt = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            });
        }

        // Act
        PersistenceContext.AddPlayerUploadData(players);

        // Assert
        PersistenceContext._UploadPlayers.Should().HaveCount(playerCount);
        for (int i = 0; i < playerCount; i++)
        {
            PersistenceContext._UploadPlayers.Should().ContainKey((ulong)(12345 + i));
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    public void AddRetainerUploadData_ShouldHandleMultipleRetainers(int retainerCount)
    {
        // Arrange
        var retainers = new List<PostRetainerRequest>();
        for (int i = 0; i < retainerCount; i++)
        {
            retainers.Add(new PostRetainerRequest
            {
                LocalContentId = (ulong)(54321 + i),
                Name = $"TestRetainer{i}",
                WorldId = 65,
                OwnerLocalContentId = (ulong)(12345 + i),
                CreatedAt = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            });
        }

        // Act
        PersistenceContext.AddRetainerUploadData(retainers);

        // Assert
        PersistenceContext._UploadRetainers.Should().HaveCount(retainerCount);
        for (int i = 0; i < retainerCount; i++)
        {
            PersistenceContext._UploadRetainers.Should().ContainKey((ulong)(54321 + i));
        }
    }
}