using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using AlphaScope.Services;

namespace AlphaScope.Tests.Services;

/// <summary>
/// Tests for CollectiblesApiService to ensure proper API integration and fallback mechanisms
/// </summary>
public class CollectiblesApiServiceTests
{
    private readonly ILogger<CollectiblesApiService> _logger;
    private readonly IMemoryCache _memoryCache;
    private readonly CollectiblesApiService _service;

    public CollectiblesApiServiceTests()
    {
        _logger = Substitute.For<ILogger<CollectiblesApiService>>();
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _service = new CollectiblesApiService(_logger, _memoryCache);
    }

    [Fact]
    public async Task GetMountAcquisitionMethodAsync_WithNullName_ReturnsUnknown()
    {
        // Act
        var result = await _service.GetMountAcquisitionMethodAsync(null);

        // Assert
        Assert.Equal("Unknown", result);
    }

    [Fact]
    public async Task GetMinionAcquisitionMethodAsync_WithEmptyName_ReturnsUnknown()
    {
        // Act
        var result = await _service.GetMinionAcquisitionMethodAsync("");

        // Assert
        Assert.Equal("Unknown", result);
    }

    [Fact]
    public async Task GetMountAcquisitionMethodAsync_WithValidName_ReturnsFallback()
    {
        // Since we can't easily mock the HTTP client in this setup,
        // this test validates that the fallback mechanism works
        
        // Act
        var result = await _service.GetMountAcquisitionMethodAsync("Company Chocobo");

        // Assert
        // Should fall back to Utils.GetMountAcquisitionMethod when API fails/times out
        Assert.NotNull(result);
        Assert.NotEqual("Unknown", result);
    }

    [Fact]
    public async Task GetMinionAcquisitionMethodAsync_WithValidName_ReturnsFallback()
    {
        // Since we can't easily mock the HTTP client in this setup,
        // this test validates that the fallback mechanism works
        
        // Act
        var result = await _service.GetMinionAcquisitionMethodAsync("Goobbue Sproutling");

        // Assert
        // Should fall back to Utils.GetMinionAcquisitionMethod when API fails/times out
        Assert.NotNull(result);
        Assert.NotEqual("Unknown", result);
    }

    [Fact]
    public async Task GetApiStatusAsync_ReturnsStatus()
    {
        // Act
        var (isAvailable, status) = await _service.GetApiStatusAsync();

        // Assert
        Assert.NotNull(status);
        // Either API is available or we get an error status
        Assert.True(isAvailable || status.StartsWith("API Unavailable") || status.StartsWith("API Error"));
    }

    [Fact]
    public async Task RefreshCacheAsync_DoesNotThrow()
    {
        // Act & Assert
        await _service.RefreshCacheAsync(); // Should not throw
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        // Act & Assert
        _service.Dispose(); // Should not throw
    }
}