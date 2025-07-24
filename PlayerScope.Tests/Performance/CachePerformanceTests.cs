using FluentAssertions;
using Microsoft.Extensions.Logging;
using PlayerScope.API.Models;
using PlayerScope.Database;
using PlayerScope.Handlers;
using System.Collections.Concurrent;
using System.Diagnostics;
using TestUtilities;

namespace PlayerScope.Tests.Performance;

public class CachePerformanceTests : IDisposable
{
    public CachePerformanceTests()
    {
        // Clear static caches before each test
        PersistenceContext._UploadPlayers.Clear();
        PersistenceContext._UploadedPlayersCache.Clear();
        PersistenceContext._UploadRetainers.Clear();
        PersistenceContext._UploadedRetainersCache.Clear();
        PersistenceContext._playerCache.Clear();
        PersistenceContext._retainerCache.Clear();
        PersistenceContext._worldRetainerCache.Clear();
        PersistenceContext._playerWithRetainersCache.Clear();
        
        // Setup logger to prevent null reference
        PersistenceContext._logger = LoggerTestUtilities.CreateMockLogger<PersistenceContext>();
    }

    public void Dispose()
    {
        // Clean up after each test
        PersistenceContext._UploadPlayers.Clear();
        PersistenceContext._UploadedPlayersCache.Clear();
        PersistenceContext._UploadRetainers.Clear();
        PersistenceContext._UploadedRetainersCache.Clear();
        PersistenceContext._playerCache.Clear();
        PersistenceContext._retainerCache.Clear();
        PersistenceContext._worldRetainerCache.Clear();
        PersistenceContext._playerWithRetainersCache.Clear();
    }

    [Fact]
    public void PlayerCache_BulkOperations_ShouldPerformWell()
    {
        // Arrange
        const int playerCount = 10000;
        const int maxTimeMs = 1000; // 1 second

        var players = new List<PostPlayerRequest>();
        for (int i = 0; i < playerCount; i++)
        {
            players.Add(new PostPlayerRequest
            {
                LocalContentId = (ulong)(7000000 + i),
                Name = $"CachePlayer{i:D5}",
                HomeWorldId = 65,
                CurrentWorldId = 65,
                AccountId = 50000 + i,
                CreatedAt = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            });
        }

        // Act - Bulk Add
        var stopwatch = Stopwatch.StartNew();
        
        PersistenceContext.AddPlayerUploadData(players);
        
        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(maxTimeMs,
            $"Adding {playerCount} players to cache should complete within {maxTimeMs}ms");

        PersistenceContext._UploadPlayers.Should().HaveCount(playerCount);
    }

    [Fact]
    public void RetainerCache_BulkOperations_ShouldPerformWell()
    {
        // Arrange
        const int retainerCount = 10000;
        const int maxTimeMs = 1000; // 1 second

        var retainers = new List<PostRetainerRequest>();
        for (int i = 0; i < retainerCount; i++)
        {
            retainers.Add(new PostRetainerRequest
            {
                LocalContentId = (ulong)(8000000 + i),
                Name = $"CacheRetainer{i:D5}",
                WorldId = 65,
                OwnerLocalContentId = (ulong)(7000000 + (i % 1000)),
                CreatedAt = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            });
        }

        // Act - Bulk Add
        var stopwatch = Stopwatch.StartNew();
        
        PersistenceContext.AddRetainerUploadData(retainers);
        
        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(maxTimeMs,
            $"Adding {retainerCount} retainers to cache should complete within {maxTimeMs}ms");

        PersistenceContext._UploadRetainers.Should().HaveCount(retainerCount);
    }

    [Fact]
    public void CacheLookup_ShouldBeFast()
    {
        // Arrange
        const int cacheSize = 50000;
        const int lookupCount = 10000;
        const int maxTimeMs = 100; // 100ms

        // Populate cache
        for (int i = 0; i < cacheSize; i++)
        {
            PersistenceContext._playerCache[(ulong)(9000000 + i)] = new PersistenceContext.CachedPlayer
            {
                Name = $"LookupPlayer{i:D5}",
                AccountId = (ulong)(60000 + i)
            };
        }

        var random = new Random(12345); // Fixed seed for reproducible results
        var lookupKeys = new List<ulong>();
        for (int i = 0; i < lookupCount; i++)
        {
            lookupKeys.Add((ulong)(9000000 + random.Next(cacheSize)));
        }

        // Act
        var stopwatch = Stopwatch.StartNew();
        
        var foundCount = 0;
        foreach (var key in lookupKeys)
        {
            if (PersistenceContext._playerCache.TryGetValue(key, out var player))
            {
                foundCount++;
                _ = player.Name; // Access the data to ensure it's not optimized away
            }
        }
        
        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(maxTimeMs,
            $"{lookupCount} cache lookups should complete within {maxTimeMs}ms");

        foundCount.Should().Be(lookupCount, "All lookups should find their keys");
    }

    [Fact]
    public void ConcurrentCacheAccess_ShouldBeThreadSafe()
    {
        // Arrange
        const int threadCount = 10;
        const int operationsPerThread = 1000;
        const int maxTimeMs = 2000; // 2 seconds

        var barrier = new Barrier(threadCount);
        var tasks = new List<Task>();
        var exceptions = new ConcurrentBag<Exception>();

        // Act
        var stopwatch = Stopwatch.StartNew();

        for (int t = 0; t < threadCount; t++)
        {
            var threadId = t;
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    barrier.SignalAndWait(); // Synchronize start

                    for (int i = 0; i < operationsPerThread; i++)
                    {
                        var key = (ulong)(threadId * operationsPerThread + i + 10000000);
                        
                        // Alternate between reads and writes
                        if (i % 2 == 0)
                        {
                            // Write operation
                            PersistenceContext._playerCache[key] = new PersistenceContext.CachedPlayer
                            {
                                Name = $"ConcurrentPlayer{threadId}_{i}",
                                AccountId = (ulong)(70000 + threadId * 1000 + i)
                            };
                        }
                        else
                        {
                            // Read operation
                            PersistenceContext._playerCache.TryGetValue(key - 1, out var player);
                        }
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }));
        }

        Task.WaitAll(tasks.ToArray());
        stopwatch.Stop();

        // Assert
        exceptions.Should().BeEmpty("No exceptions should occur during concurrent access");
        
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(maxTimeMs,
            $"Concurrent cache operations should complete within {maxTimeMs}ms");

        // Verify that data was written correctly
        var expectedCount = threadCount * operationsPerThread / 2; // Only write operations
        PersistenceContext._playerCache.Should().HaveCountGreaterOrEqualTo(expectedCount);
    }

    [Fact]
    public void WorldRetainerCache_ComplexOperations_ShouldPerformWell()
    {
        // Arrange
        const int worldCount = 20;
        const int retainersPerWorld = 1000;
        const int maxTimeMs = 1500; // 1.5 seconds

        var stopwatch = Stopwatch.StartNew();

        // Act - Populate world retainer cache
        for (uint worldId = 65; worldId < 65 + worldCount; worldId++)
        {
            var worldCache = new ConcurrentDictionary<string, ulong>();
            
            for (int i = 0; i < retainersPerWorld; i++)
            {
                var retainerName = $"WorldRetainer{worldId}_{i:D4}";
                var ownerContentId = (ulong)(11000000 + worldId * 1000 + i);
                worldCache[retainerName] = ownerContentId;
            }
            
            PersistenceContext._worldRetainerCache[worldId] = worldCache;
        }

        stopwatch.Stop();

        // Assert - Creation performance
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(maxTimeMs,
            $"Populating world retainer cache should complete within {maxTimeMs}ms");

        PersistenceContext._worldRetainerCache.Should().HaveCount(worldCount);

        // Act - Lookup performance test
        var random = new Random(54321);
        const int lookupCount = 5000;
        const int maxLookupTimeMs = 500;

        stopwatch.Restart();

        var foundCount = 0;
        for (int i = 0; i < lookupCount; i++)
        {
            var worldId = (uint)(65 + random.Next(worldCount));
            var retainerIndex = random.Next(retainersPerWorld);
            var retainerName = $"WorldRetainer{worldId}_{retainerIndex:D4}";

            if (PersistenceContext._worldRetainerCache.TryGetValue(worldId, out var worldCache) &&
                worldCache.TryGetValue(retainerName, out var ownerContentId))
            {
                foundCount++;
            }
        }

        stopwatch.Stop();

        // Assert - Lookup performance
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(maxLookupTimeMs,
            $"{lookupCount} world retainer lookups should complete within {maxLookupTimeMs}ms");

        foundCount.Should().Be(lookupCount, "All lookups should find their retainers");
    }

    [Fact]
    public void UpdateRetainers_ShouldPerformWellWithLargeDataset()
    {
        // Arrange
        const int playerCount = 1000;
        const int retainersPerPlayer = 5;
        const int maxTimeMs = 3000; // 3 seconds

        // Populate player cache
        for (int i = 0; i < playerCount; i++)
        {
            var contentId = (ulong)(12000000 + i);
            PersistenceContext._playerCache[contentId] = new PersistenceContext.CachedPlayer
            {
                Name = $"UpdatePlayer{i:D4}",
                AccountId = (ulong)(80000 + i)
            };
        }

        // Populate retainer cache
        for (int i = 0; i < playerCount; i++)
        {
            var ownerContentId = (ulong)(12000000 + i);
            
            for (int j = 0; j < retainersPerPlayer; j++)
            {
                var retainerContentId = (ulong)(13000000 + i * retainersPerPlayer + j);
                PersistenceContext._retainerCache[retainerContentId] = new Retainer
                {
                    LocalContentId = retainerContentId,
                    Name = $"UpdateRetainer{i:D4}_{j}",
                    WorldId = 65,
                    OwnerLocalContentId = ownerContentId
                };
            }
        }

        // Act
        var stopwatch = Stopwatch.StartNew();
        
        PersistenceContext.UpdateRetainers();
        
        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(maxTimeMs,
            $"UpdateRetainers with {playerCount * retainersPerPlayer} retainers should complete within {maxTimeMs}ms");

        PersistenceContext._playerWithRetainersCache.Should().HaveCount(playerCount);
        
        // Verify a few entries to ensure correctness
        var firstPlayer = PersistenceContext._playerWithRetainersCache[12000000UL];
        firstPlayer.Retainers.Should().HaveCount(retainersPerPlayer);
    }

    [Fact]
    public void MemoryUsage_ShouldRemainReasonableForLargeCaches()
    {
        // Arrange
        const int cacheSize = 100000;
        const long maxMemoryIncreaseBytes = 200 * 1024 * 1024; // 200MB

        var initialMemory = GC.GetTotalMemory(true);

        // Act - Fill caches with substantial data
        for (int i = 0; i < cacheSize; i++)
        {
            var contentId = (ulong)(14000000 + i);
            
            // Player cache
            PersistenceContext._playerCache[contentId] = new PersistenceContext.CachedPlayer
            {
                Name = $"MemoryTestPlayer{i:D6}",
                AccountId = (ulong)(90000 + i)
            };

            // Retainer cache
            PersistenceContext._retainerCache[contentId + cacheSize] = new Retainer
            {
                LocalContentId = contentId + cacheSize,
                Name = $"MemoryTestRetainer{i:D6}",
                WorldId = 65,
                OwnerLocalContentId = contentId
            };
        }

        var finalMemory = GC.GetTotalMemory(true);
        var memoryIncrease = finalMemory - initialMemory;

        // Assert
        memoryIncrease.Should().BeLessThan(maxMemoryIncreaseBytes,
            $"Memory usage should not increase by more than {maxMemoryIncreaseBytes / (1024 * 1024)}MB for {cacheSize} cache entries");

        PersistenceContext._playerCache.Should().HaveCount(cacheSize);
        PersistenceContext._retainerCache.Should().HaveCount(cacheSize);
    }
}