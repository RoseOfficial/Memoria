using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using AlphaScope.Services.Models;

namespace AlphaScope.Services;

/// <summary>
/// Service for fetching mount and minion acquisition data from FFXIVCollect API
/// with caching and fallback mechanisms
/// </summary>
public sealed class CollectiblesApiService : ICollectiblesAcquisitionService, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<CollectiblesApiService> _logger;
    private readonly SemaphoreSlim _semaphore;
    
    private const string BaseUrl = "https://ffxivcollect.com/api/";
    private const int RequestTimeoutSeconds = 30;
    private const int CacheHours = 24;
    
    // Cache keys
    private const string MountsCacheKey = "ffxivcollect_mounts";
    private const string MinionsCacheKey = "ffxivcollect_minions";
    private const string AcquisitionDataCacheKey = "acquisition_data";
    
    public CollectiblesApiService(ILogger<CollectiblesApiService> logger, IMemoryCache cache)
    {
        _logger = logger;
        _cache = cache;
        _semaphore = new SemaphoreSlim(1, 1);
        
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(RequestTimeoutSeconds),
            BaseAddress = new Uri(BaseUrl)
        };
        
        // Set headers - FFXIVCollect seems to prefer simpler headers
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "AlphaScope-FFXIV-Plugin/1.0");
        // Don't set Accept header explicitly - let it default to */*
        // FFXIVCollect returns 406 with explicit application/json Accept header
        
        _logger.LogInformation("CollectiblesApiService initialized with base URL: {BaseUrl}", BaseUrl);
        
        // Start background initialization to pre-populate cache
        _ = Task.Run(async () => 
        {
            try
            {
                _logger.LogInformation("Starting background initialization of collectibles data...");
                await GetCachedAcquisitionDataAsync();
                _logger.LogInformation("Background initialization of collectibles data completed");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to initialize collectibles data in background");
            }
        });
    }
    
    /// <summary>
    /// Gets mount acquisition method from API with caching and fallback
    /// </summary>
    public async Task<string> GetMountAcquisitionMethodAsync(string? mountName)
    {
        if (string.IsNullOrWhiteSpace(mountName))
            return "Unknown";
            
        try
        {
            var acquisitionData = await GetCachedAcquisitionDataAsync();
            
            if (acquisitionData.MountAcquisitions.TryGetValue(mountName, out var method))
            {
                _logger.LogDebug("Found mount acquisition for '{MountName}': {Method} (from {Source})", 
                    mountName, method, acquisitionData.IsFromApi ? "API" : "fallback");
                return method;
            }
            
            // Try partial matching
            foreach (var kvp in acquisitionData.MountAcquisitions)
            {
                if (mountName.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogDebug("Found partial mount match for '{MountName}': {Method}", mountName, kvp.Value);
                    return kvp.Value;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get mount acquisition for '{MountName}'", mountName);
        }
        
        // Final fallback to static data
        return Utils.GetMountAcquisitionMethod(mountName);
    }
    
    /// <summary>
    /// Gets minion acquisition method from API with caching and fallback
    /// </summary>
    public async Task<string> GetMinionAcquisitionMethodAsync(string? minionName)
    {
        if (string.IsNullOrWhiteSpace(minionName))
            return "Unknown";
            
        try
        {
            var acquisitionData = await GetCachedAcquisitionDataAsync();
            
            if (acquisitionData.MinionAcquisitions.TryGetValue(minionName, out var method))
            {
                _logger.LogDebug("Found minion acquisition for '{MinionName}': {Method} (from {Source})", 
                    minionName, method, acquisitionData.IsFromApi ? "API" : "fallback");
                return method;
            }
            
            // Try partial matching
            foreach (var kvp in acquisitionData.MinionAcquisitions)
            {
                if (minionName.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogDebug("Found partial minion match for '{MinionName}': {Method}", minionName, kvp.Value);
                    return kvp.Value;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get minion acquisition for '{MinionName}'", minionName);
        }
        
        // Final fallback to static data
        return Utils.GetMinionAcquisitionMethod(minionName);
    }
    
    /// <summary>
    /// Forces a refresh of the cached acquisition data
    /// </summary>
    public async Task RefreshCacheAsync()
    {
        _logger.LogInformation("Forcing refresh of collectibles acquisition data");
        
        _cache.Remove(AcquisitionDataCacheKey);
        await GetCachedAcquisitionDataAsync();
    }
    
    /// <summary>
    /// Gets cached acquisition data, refreshing from API if needed
    /// </summary>
    private async Task<CachedAcquisitionData> GetCachedAcquisitionDataAsync()
    {
        // Check cache first
        if (_cache.TryGetValue(AcquisitionDataCacheKey, out CachedAcquisitionData? cachedData) && cachedData != null)
        {
            return cachedData;
        }
        
        // Use semaphore to prevent multiple concurrent API calls
        await _semaphore.WaitAsync();
        try
        {
            // Double-check cache after acquiring semaphore
            if (_cache.TryGetValue(AcquisitionDataCacheKey, out cachedData) && cachedData != null)
            {
                return cachedData;
            }
            
            _logger.LogInformation("Fetching fresh acquisition data from FFXIVCollect API");
            
            var acquisitionData = new CachedAcquisitionData
            {
                LastUpdated = DateTime.UtcNow
            };
            
            try
            {
                // Fetch both mounts and minions concurrently
                var mountTask = FetchMountsAsync();
                var minionTask = FetchMinionsAsync();
                
                await Task.WhenAll(mountTask, minionTask);
                
                var mounts = await mountTask;
                var minions = await minionTask;
                
                // Process mounts
                foreach (var mount in mounts)
                {
                    var method = ExtractAcquisitionMethod(mount.Sources);
                    if (!string.IsNullOrEmpty(method))
                    {
                        acquisitionData.MountAcquisitions[mount.Name] = method;
                    }
                }
                
                // Process minions
                foreach (var minion in minions)
                {
                    var method = ExtractAcquisitionMethod(minion.Sources);
                    if (!string.IsNullOrEmpty(method))
                    {
                        acquisitionData.MinionAcquisitions[minion.Name] = method;
                    }
                }
                
                acquisitionData.IsFromApi = true;
                
                _logger.LogInformation("Successfully fetched acquisition data: {MountCount} mounts, {MinionCount} minions", 
                    acquisitionData.MountAcquisitions.Count, acquisitionData.MinionAcquisitions.Count);
            }
            catch (Exception ex)
            {
                // Don't dump the full stack — FFXIVCollect changes their JSON schema periodically
                // and we have a static fallback. Short warning is enough signal.
                _logger.LogWarning("Failed to fetch data from FFXIVCollect API ({Error}); using fallback data", ex.Message);
                
                // Use static data as fallback
                acquisitionData.IsFromApi = false;
                PopulateFallbackData(acquisitionData);
            }
            
            // Cache for specified duration
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(CacheHours),
                Priority = CacheItemPriority.High
            };
            
            _cache.Set(AcquisitionDataCacheKey, acquisitionData, cacheOptions);
            return acquisitionData;
        }
        finally
        {
            _semaphore.Release();
        }
    }
    
    /// <summary>
    /// Fetches all mounts from FFXIVCollect API
    /// </summary>
    private async Task<List<FFXIVCollectMount>> FetchMountsAsync()
    {
        var allMounts = new List<FFXIVCollectMount>();
        var url = "mounts"; // Remove leading slash for proper BaseAddress concatenation
        
        while (!string.IsNullOrEmpty(url))
        {
            _logger.LogDebug("Fetching mounts from: {Url}", url);
            
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            
            var json = await response.Content.ReadAsStringAsync();
            
            // Check if response is HTML instead of JSON
            if (json.TrimStart().StartsWith("<"))
            {
                _logger.LogError("API returned HTML instead of JSON");
                throw new InvalidOperationException("API returned HTML instead of JSON - possible redirect or error page");
            }
            
            var result = JsonSerializer.Deserialize<FFXIVCollectResponse<FFXIVCollectMount>>(json);
            
            if (result?.Results != null)
            {
                allMounts.AddRange(result.Results);
            }
            
            // Handle pagination
            url = result?.Next;
            if (!string.IsNullOrEmpty(url))
            {
                if (url.StartsWith("http"))
                {
                    // Convert absolute URL to relative, removing the /api/ prefix
                    var uri = new Uri(url);
                    url = uri.PathAndQuery;
                    if (url.StartsWith("/api/"))
                        url = url.Substring(5); // Remove "/api/" prefix
                    else if (url.StartsWith("/"))
                        url = url.Substring(1); // Remove leading slash
                }
            }
            
            // Add delay to be respectful
            if (!string.IsNullOrEmpty(url))
            {
                await Task.Delay(100);
            }
        }
        
        _logger.LogInformation("Fetched {Count} mounts from FFXIVCollect API", allMounts.Count);
        return allMounts;
    }
    
    /// <summary>
    /// Fetches all minions from FFXIVCollect API
    /// </summary>
    private async Task<List<FFXIVCollectMinion>> FetchMinionsAsync()
    {
        var allMinions = new List<FFXIVCollectMinion>();
        var url = "minions"; // Remove leading slash for proper BaseAddress concatenation
        
        while (!string.IsNullOrEmpty(url))
        {
            _logger.LogDebug("Fetching minions from: {Url}", url);
            
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            
            var json = await response.Content.ReadAsStringAsync();
            
            // Check if response is HTML instead of JSON
            if (json.TrimStart().StartsWith("<"))
            {
                _logger.LogError("API returned HTML instead of JSON");
                throw new InvalidOperationException("API returned HTML instead of JSON - possible redirect or error page");
            }
            
            var result = JsonSerializer.Deserialize<FFXIVCollectResponse<FFXIVCollectMinion>>(json);
            
            if (result?.Results != null)
            {
                allMinions.AddRange(result.Results);
            }
            
            // Handle pagination
            url = result?.Next;
            if (!string.IsNullOrEmpty(url))
            {
                if (url.StartsWith("http"))
                {
                    // Convert absolute URL to relative, removing the /api/ prefix
                    var uri = new Uri(url);
                    url = uri.PathAndQuery;
                    if (url.StartsWith("/api/"))
                        url = url.Substring(5); // Remove "/api/" prefix
                    else if (url.StartsWith("/"))
                        url = url.Substring(1); // Remove leading slash
                }
            }
            
            // Add delay to be respectful
            if (!string.IsNullOrEmpty(url))
            {
                await Task.Delay(100);
            }
        }
        
        _logger.LogInformation("Fetched {Count} minions from FFXIVCollect API", allMinions.Count);
        return allMinions;
    }
    
    /// <summary>
    /// Extracts acquisition method from sources array
    /// </summary>
    private string ExtractAcquisitionMethod(List<CollectibleSource>? sources)
    {
        if (sources == null || sources.Count == 0)
            return "Unknown";
            
        var primarySource = sources.First();
        
        // Get the category type
        var category = primarySource.Type.ToLowerInvariant().Replace(" ", "").Replace("-", "") switch
        {
            "quest" => "Quest",
            "achievement" => "Achievement",
            "dungeon" => "Dungeon",
            "trial" => "Trial",
            "raid" => "Raid",
            "marketboard" or "purchase" => "Market Board",
            "pvp" => "PvP",
            "event" or "seasonalevent" => "Event",
            "goldsaucer" or "mgp" => "Gold Saucer",
            "venture" or "retainerventure" => "Venture",
            "deepdungeon" or "potd" or "hoh" => "Deep Dungeon",
            "treasure" or "treasurehunt" => "Treasure",
            "preorder" or "collectoredition" => "Pre-order",
            "special" or "specialquest" => "Special",
            "crafting" or "crafted" => "Crafted",
            "fates" or "fate" => "FATE",
            "eureka" => "Eureka",
            "bozja" or "bozjan" => "Bozja",
            _ => primarySource.Type
        };
        
        // If we have specific text, combine category with details
        if (!string.IsNullOrWhiteSpace(primarySource.Text))
        {
            // Truncate text if too long
            var details = primarySource.Text.Length > 35 
                ? primarySource.Text.Substring(0, 32) + "..." 
                : primarySource.Text;
            
            return $"{category} - {details}";
        }
        
        // Return just the category if no specific text
        return category;
    }
    
    /// <summary>
    /// Populates fallback data using static Utils methods
    /// </summary>
    private void PopulateFallbackData(CachedAcquisitionData acquisitionData)
    {
        // This would ideally use reflection to get the static data from Utils,
        // but for now we'll leave it empty and let the individual methods fall back
        _logger.LogInformation("Using fallback to static acquisition data");
    }
    
    /// <summary>
    /// Gets API status information
    /// </summary>
    public async Task<(bool IsAvailable, string Status)> GetApiStatusAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("mounts?limit=1");
            if (response.IsSuccessStatusCode)
            {
                return (true, "API Available");
            }
            else
            {
                return (false, $"API Error: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            return (false, $"API Unavailable: {ex.Message}");
        }
    }
    
    public void Dispose()
    {
        _httpClient?.Dispose();
        _semaphore?.Dispose();
        _logger.LogDebug("CollectiblesApiService disposed");
    }
}