using FluentAssertions;
using PlayerScope.API.Models;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;

namespace PlayerScope.Tests.Performance;

public class ApiPerformanceSimulationTests
{
    [Fact]
    public void JsonSerialization_PlayerData_ShouldPerformWell()
    {
        // Arrange
        const int playerCount = 5000;
        const int maxTimeMs = 2000; // 2 seconds

        var players = new List<PostPlayerRequest>();
        for (int i = 0; i < playerCount; i++)
        {
            players.Add(new PostPlayerRequest
            {
                LocalContentId = (ulong)(15000000 + i),
                Name = $"SerializationPlayer{i:D5}",
                HomeWorldId = (ushort)(65 + (i % 10)),
                CurrentWorldId = (ushort)(65 + (i % 10)),
                AccountId = 100000 + i,
                CreatedAt = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            });
        }

        // Act - Serialize
        var stopwatch = Stopwatch.StartNew();
        
        var json = JsonSerializer.Serialize(players);
        
        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(maxTimeMs,
            $"Serializing {playerCount} players should complete within {maxTimeMs}ms");

        json.Should().NotBeEmpty();
        json.Length.Should().BeGreaterThan(1000);
    }

    [Fact]
    public void JsonDeserialization_PlayerData_ShouldPerformWell()
    {
        // Arrange
        const int playerCount = 5000;
        const int maxTimeMs = 2000; // 2 seconds

        var players = new List<PostPlayerRequest>();
        for (int i = 0; i < playerCount; i++)
        {
            players.Add(new PostPlayerRequest
            {
                LocalContentId = (ulong)(16000000 + i),
                Name = $"DeserializationPlayer{i:D5}",
                HomeWorldId = (ushort)(65 + (i % 10)),
                CurrentWorldId = (ushort)(65 + (i % 10)),
                AccountId = 110000 + i,
                CreatedAt = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            });
        }

        var json = JsonSerializer.Serialize(players);

        // Act - Deserialize
        var stopwatch = Stopwatch.StartNew();
        
        var deserializedPlayers = JsonSerializer.Deserialize<List<PostPlayerRequest>>(json);
        
        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(maxTimeMs,
            $"Deserializing {playerCount} players should complete within {maxTimeMs}ms");

        deserializedPlayers.Should().NotBeNull();
        deserializedPlayers!.Should().HaveCount(playerCount);
    }

    [Fact]
    public void ConcurrentDataProcessing_ShouldHandleMultipleThreads()
    {
        // Arrange
        const int threadCount = 8;
        const int itemsPerThread = 1000;
        const int maxTimeMs = 3000; // 3 seconds

        var results = new ConcurrentBag<PostPlayerRequest>();
        var tasks = new List<Task>();
        var barrier = new Barrier(threadCount);

        // Act
        var stopwatch = Stopwatch.StartNew();

        for (int t = 0; t < threadCount; t++)
        {
            var threadId = t;
            tasks.Add(Task.Run(() =>
            {
                barrier.SignalAndWait(); // Synchronize start

                for (int i = 0; i < itemsPerThread; i++)
                {
                    var player = new PostPlayerRequest
                    {
                        LocalContentId = (ulong)(17000000 + threadId * itemsPerThread + i),
                        Name = $"ConcurrentPlayer{threadId}_{i:D4}",
                        HomeWorldId = (ushort)(65 + threadId),
                        CurrentWorldId = (ushort)(65 + threadId),
                        AccountId = 120000 + threadId * 1000 + i,
                        CreatedAt = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                    };

                    // Simulate some processing work
                    var json = JsonSerializer.Serialize(player);
                    var deserialized = JsonSerializer.Deserialize<PostPlayerRequest>(json);
                    
                    if (deserialized != null)
                    {
                        results.Add(deserialized);
                    }
                }
            }));
        }

        Task.WaitAll(tasks.ToArray());
        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(maxTimeMs,
            $"Concurrent processing should complete within {maxTimeMs}ms");

        results.Should().HaveCount(threadCount * itemsPerThread);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(1000)]
    [InlineData(5000)]
    public void BatchProcessing_ShouldScaleWithDataSize(int batchSize)
    {
        // Arrange
        const int maxTimePerItemMs = 2; // 2ms per item maximum
        var expectedMaxTime = batchSize * maxTimePerItemMs;

        var players = new List<PostPlayerRequest>();
        for (int i = 0; i < batchSize; i++)
        {
            players.Add(new PostPlayerRequest
            {
                LocalContentId = (ulong)(18000000 + i),
                Name = $"BatchPlayer{i:D5}",
                HomeWorldId = 65,
                CurrentWorldId = 65,
                AccountId = 130000 + i,
                CreatedAt = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            });
        }

        // Act - Simulate batch processing
        var stopwatch = Stopwatch.StartNew();
        
        var processedCount = 0;
        foreach (var player in players)
        {
            // Simulate validation and processing
            if (!string.IsNullOrEmpty(player.Name) && player.LocalContentId > 0)
            {
                var json = JsonSerializer.Serialize(player);
                if (json.Length > 10) // Basic validation
                {
                    processedCount++;
                }
            }
        }
        
        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(expectedMaxTime,
            $"Batch processing {batchSize} items should complete within {expectedMaxTime}ms");

        processedCount.Should().Be(batchSize);
    }

    [Fact]
    public void DataTransformation_ShouldPerformWellWithComplexObjects()
    {
        // Arrange
        const int objectCount = 2000;
        const int maxTimeMs = 1500; // 1.5 seconds

        var players = new List<PostPlayerRequest>();
        var retainers = new List<PostRetainerRequest>();

        // Create players
        for (int i = 0; i < objectCount; i++)
        {
            players.Add(new PostPlayerRequest
            {
                LocalContentId = (ulong)(19000000 + i),
                Name = $"TransformPlayer{i:D4}",
                HomeWorldId = (ushort)(65 + (i % 5)),
                CurrentWorldId = (ushort)(65 + (i % 5)),
                AccountId = 140000 + i,
                CreatedAt = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            });
        }

        // Create retainers
        for (int i = 0; i < objectCount; i++)
        {
            retainers.Add(new PostRetainerRequest
            {
                LocalContentId = (ulong)(20000000 + i),
                Name = $"TransformRetainer{i:D4}",
                WorldId = (ushort)(65 + (i % 5)),
                OwnerLocalContentId = (ulong)(19000000 + (i % 100)), // Link to players
                CreatedAt = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            });
        }

        // Act - Transform and group data
        var stopwatch = Stopwatch.StartNew();
        
        var playerLookup = players.ToDictionary(p => p.LocalContentId, p => p);
        var groupedRetainers = retainers
            .GroupBy(r => r.OwnerLocalContentId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var combined = new List<object>();
        foreach (var player in players)
        {
            var playerRetainers = groupedRetainers.GetValueOrDefault(player.LocalContentId, new List<PostRetainerRequest>());
            combined.Add(new
            {
                Player = player,
                RetainerCount = playerRetainers.Count,
                Retainers = playerRetainers.Take(3).ToList() // Limit for performance
            });
        }
        
        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(maxTimeMs,
            $"Data transformation should complete within {maxTimeMs}ms");

        combined.Should().HaveCount(objectCount);
        playerLookup.Should().HaveCount(objectCount);
    }

    [Fact]
    public void MemoryUsage_ShouldBeReasonableForLargeDatasets()
    {
        // Arrange
        const int dataSize = 10000;
        const long maxMemoryIncreaseBytes = 150 * 1024 * 1024; // 150MB

        var initialMemory = GC.GetTotalMemory(true);

        // Act - Create large dataset
        var players = new List<PostPlayerRequest>();
        var retainers = new List<PostRetainerRequest>();

        for (int i = 0; i < dataSize; i++)
        {
            players.Add(new PostPlayerRequest
            {
                LocalContentId = (ulong)(21000000 + i),
                Name = $"MemoryPlayer{i:D5}",
                HomeWorldId = (ushort)(65 + (i % 10)),
                CurrentWorldId = (ushort)(65 + (i % 10)),
                AccountId = 150000 + i,
                CreatedAt = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            });

            // Create 2-3 retainers per player
            for (int j = 0; j < 2 + (i % 2); j++)
            {
                retainers.Add(new PostRetainerRequest
                {
                    LocalContentId = (ulong)(22000000 + i * 3 + j),
                    Name = $"MemoryRetainer{i:D5}_{j}",
                    WorldId = (ushort)(65 + (i % 10)),
                    OwnerLocalContentId = (ulong)(21000000 + i),
                    CreatedAt = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                });
            }
        }

        var finalMemory = GC.GetTotalMemory(true);
        var memoryIncrease = finalMemory - initialMemory;

        // Assert
        memoryIncrease.Should().BeLessThan(maxMemoryIncreaseBytes,
            $"Memory usage should not increase by more than {maxMemoryIncreaseBytes / (1024 * 1024)}MB");

        players.Should().HaveCount(dataSize);
        retainers.Should().HaveCountGreaterThan(dataSize * 2); // At least 2 per player
    }

    [Fact]
    public void HighFrequencyOperations_ShouldMaintainPerformance()
    {
        // Arrange
        const int operationCount = 50000;
        const int maxTimeMs = 2000; // 2 seconds

        var players = new ConcurrentDictionary<ulong, PostPlayerRequest>();

        // Act - High frequency add/remove operations
        var stopwatch = Stopwatch.StartNew();
        
        for (int i = 0; i < operationCount; i++)
        {
            var contentId = (ulong)(23000000 + i);
            
            // Add
            players[contentId] = new PostPlayerRequest
            {
                LocalContentId = contentId,
                Name = $"HighFreqPlayer{i:D5}",
                HomeWorldId = 65,
                CurrentWorldId = 65,
                AccountId = 160000 + i,
                CreatedAt = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            // Occasional removal to simulate cleanup
            if (i % 100 == 0 && i > 0)
            {
                players.TryRemove((ulong)(23000000 + i - 50), out _);
            }
        }
        
        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(maxTimeMs,
            $"{operationCount} high-frequency operations should complete within {maxTimeMs}ms");

        players.Should().HaveCountGreaterThan((int)(operationCount * 0.9)); // Account for removals
    }
}