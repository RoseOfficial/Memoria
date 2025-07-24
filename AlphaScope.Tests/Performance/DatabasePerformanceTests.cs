using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using AlphaScope.Database;
using System.Diagnostics;
using TestUtilities;

namespace AlphaScope.Tests.Performance;

public class DatabasePerformanceTests : IDisposable
{
    private readonly RetainerTrackContext _context;

    public DatabasePerformanceTests()
    {
        var options = DatabaseTestUtilities.CreateInMemoryDbOptions<RetainerTrackContext>();
        _context = new RetainerTrackContext(options);
        _context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _context?.Dispose();
    }

    [Fact]
    public async Task BulkPlayerInsert_ShouldCompleteWithinTimeLimit()
    {
        // Arrange
        const int playerCount = 1000;
        const int maxTimeMilliseconds = 5000; // 5 seconds

        var players = new List<Player>();
        for (int i = 0; i < playerCount; i++)
        {
            players.Add(new Player
            {
                LocalContentId = (ulong)(100000 + i),
                Name = $"TestPlayer{i}",
                AccountId = (ulong?)(1000 + i)
            });
        }

        // Act
        var stopwatch = Stopwatch.StartNew();
        
        _context.Players.AddRange(players);
        await _context.SaveChangesAsync();
        
        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(maxTimeMilliseconds, 
            $"Bulk insert of {playerCount} players should complete within {maxTimeMilliseconds}ms");
        
        var savedPlayers = await _context.Players.CountAsync();
        savedPlayers.Should().Be(playerCount);
    }

    [Fact]
    public async Task BulkRetainerInsert_ShouldCompleteWithinTimeLimit()
    {
        // Arrange
        const int retainerCount = 1000;
        const int maxTimeMilliseconds = 5000; // 5 seconds

        var retainers = new List<Retainer>();
        for (int i = 0; i < retainerCount; i++)
        {
            retainers.Add(new Retainer
            {
                LocalContentId = (ulong)(200000 + i),
                Name = $"TestRetainer{i}",
                WorldId = (ushort)(65 + (i % 10)),
                OwnerLocalContentId = (ulong)(100000 + (i % 100))
            });
        }

        // Act
        var stopwatch = Stopwatch.StartNew();
        
        _context.Retainers.AddRange(retainers);
        await _context.SaveChangesAsync();
        
        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(maxTimeMilliseconds,
            $"Bulk insert of {retainerCount} retainers should complete within {maxTimeMilliseconds}ms");
        
        var savedRetainers = await _context.Retainers.CountAsync();
        savedRetainers.Should().Be(retainerCount);
    }

    [Fact]
    public async Task PlayerQuery_ByName_ShouldCompleteWithinTimeLimit()
    {
        // Arrange
        const int playerCount = 1000;
        const int maxTimeMilliseconds = 1000; // 1 second

        // Seed database with test data
        var players = new List<Player>();
        for (int i = 0; i < playerCount; i++)
        {
            players.Add(new Player
            {
                LocalContentId = (ulong)(300000 + i),
                Name = $"QueryPlayer{i:D4}",
                AccountId = (ulong)(2000 + i)
            });
        }

        _context.Players.AddRange(players);
        await _context.SaveChangesAsync();

        // Act
        var stopwatch = Stopwatch.StartNew();
        
        var results = await _context.Players
            .Where(p => p.Name.Contains("QueryPlayer05"))
            .ToListAsync();
        
        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(maxTimeMilliseconds,
            $"Player name query should complete within {maxTimeMilliseconds}ms");
        
        results.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public async Task RetainerQuery_ByOwner_ShouldCompleteWithinTimeLimit()
    {
        // Arrange
        const int retainerCount = 1000;
        const ulong testOwnerId = 400000;
        const int maxTimeMilliseconds = 1000; // 1 second

        // Seed database with test data
        var retainers = new List<Retainer>();
        for (int i = 0; i < retainerCount; i++)
        {
            retainers.Add(new Retainer
            {
                LocalContentId = (ulong)(500000 + i),
                Name = $"QueryRetainer{i:D4}",
                WorldId = 65,
                OwnerLocalContentId = i < 50 ? testOwnerId : (ulong)(400000 + i) // First 50 belong to testOwnerId
            });
        }

        _context.Retainers.AddRange(retainers);
        await _context.SaveChangesAsync();

        // Act
        var stopwatch = Stopwatch.StartNew();
        
        var results = await _context.Retainers
            .Where(r => r.OwnerLocalContentId == testOwnerId)
            .ToListAsync();
        
        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(maxTimeMilliseconds,
            $"Retainer owner query should complete within {maxTimeMilliseconds}ms");
        
        results.Should().HaveCount(50);
    }

    [Fact]
    public async Task ComplexJoinQuery_ShouldCompleteWithinTimeLimit()
    {
        // Arrange
        const int playerCount = 500;
        const int retainersPerPlayer = 3;
        const int maxTimeMilliseconds = 2000; // 2 seconds

        // Seed players
        var players = new List<Player>();
        for (int i = 0; i < playerCount; i++)
        {
            players.Add(new Player
            {
                LocalContentId = (ulong)(600000 + i),
                Name = $"JoinPlayer{i:D3}",
                AccountId = (ulong)(3000 + i)
            });
        }

        _context.Players.AddRange(players);
        await _context.SaveChangesAsync();

        // Seed retainers
        var retainers = new List<Retainer>();
        for (int i = 0; i < playerCount; i++)
        {
            for (int j = 0; j < retainersPerPlayer; j++)
            {
                retainers.Add(new Retainer
                {
                    LocalContentId = (ulong)(700000 + (i * retainersPerPlayer) + j),
                    Name = $"JoinRetainer{i:D3}_{j}",
                    WorldId = 65,
                    OwnerLocalContentId = (ulong)(600000 + i)
                });
            }
        }

        _context.Retainers.AddRange(retainers);
        await _context.SaveChangesAsync();

        // Act
        var stopwatch = Stopwatch.StartNew();
        
        var results = await _context.Players
            .Where(p => p.Name.Contains("JoinPlayer1"))
            .Select(p => new
            {
                Player = p,
                RetainerCount = _context.Retainers.Count(r => r.OwnerLocalContentId == p.LocalContentId)
            })
            .ToListAsync();
        
        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(maxTimeMilliseconds,
            $"Complex join query should complete within {maxTimeMilliseconds}ms");
        
        results.Should().HaveCountGreaterThan(0);
        results.First().RetainerCount.Should().Be(retainersPerPlayer);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(500)]
    [InlineData(1000)]
    public async Task DatabaseOperations_ShouldScaleLinearlyWithDataSize(int recordCount)
    {
        // Arrange
        const int maxTimePerRecordMs = 10; // 10ms per record maximum
        var expectedMaxTime = recordCount * maxTimePerRecordMs;

        var players = new List<Player>();
        for (int i = 0; i < recordCount; i++)
        {
            players.Add(new Player
            {
                LocalContentId = (ulong)(800000 + i),
                Name = $"ScalePlayer{i:D4}",
                AccountId = (ulong)(4000 + i)
            });
        }

        // Act & Assert - Insert
        var stopwatch = Stopwatch.StartNew();
        _context.Players.AddRange(players);
        await _context.SaveChangesAsync();
        stopwatch.Stop();

        stopwatch.ElapsedMilliseconds.Should().BeLessThan(expectedMaxTime,
            $"Insert of {recordCount} records should complete within {expectedMaxTime}ms");

        // Act & Assert - Query
        stopwatch.Restart();
        var queryResults = await _context.Players
            .Where(p => p.LocalContentId >= 800000)
            .ToListAsync();
        stopwatch.Stop();

        stopwatch.ElapsedMilliseconds.Should().BeLessThan(expectedMaxTime / 2,
            $"Query of {recordCount} records should complete within {expectedMaxTime / 2}ms");

        queryResults.Should().HaveCount(recordCount);
    }

    [Fact]
    public async Task MemoryUsage_ShouldRemainReasonableForLargeDatasets()
    {
        // Arrange
        const int recordCount = 5000;
        const long maxMemoryIncreaseBytes = 100 * 1024 * 1024; // 100MB

        var initialMemory = GC.GetTotalMemory(true);

        // Act
        var players = new List<Player>();
        for (int i = 0; i < recordCount; i++)
        {
            players.Add(new Player
            {
                LocalContentId = (ulong)(900000 + i),
                Name = $"MemoryPlayer{i:D4}",
                AccountId = (ulong)(5000 + i)
            });
        }

        _context.Players.AddRange(players);
        await _context.SaveChangesAsync();

        var finalMemory = GC.GetTotalMemory(true);
        var memoryIncrease = finalMemory - initialMemory;

        // Assert
        memoryIncrease.Should().BeLessThan(maxMemoryIncreaseBytes,
            $"Memory usage should not increase by more than {maxMemoryIncreaseBytes / (1024 * 1024)}MB for {recordCount} records");

        var savedCount = await _context.Players.CountAsync();
        savedCount.Should().BeGreaterOrEqualTo(recordCount);
    }
}