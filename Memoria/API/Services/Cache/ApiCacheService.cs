using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Memoria.API.Abstractions.Cache;
using Memoria.API.Models.Shared;
using Memoria.API.Constants;

namespace Memoria.API.Services.Cache
{
    /// <summary>
    /// Professional caching service implementation providing efficient data storage and retrieval operations.
    /// Uses MemoryCache for in-memory caching with comprehensive statistics tracking and intelligent cache management.
    /// Thread-safe implementation with support for various expiration policies and cache warming capabilities.
    /// </summary>
    public class ApiCacheService : IApiCacheService, IDisposable
    {
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<ApiCacheService> _logger;
        private readonly CacheStatistics _statistics;
        private readonly object _statisticsLock = new object();
        private readonly SemaphoreSlim _semaphore;
        private readonly Dictionary<string, object> _keyTracker;
        private readonly object _keyTrackerLock = new object();
        private bool _disposed = false;

        /// <summary>
        /// Default absolute expiration time for cached items (15 minutes)
        /// </summary>
        public static readonly TimeSpan DefaultAbsoluteExpiration = TimeSpan.FromMinutes(15);

        /// <summary>
        /// Default sliding expiration time for cached items (5 minutes)
        /// </summary>
        public static readonly TimeSpan DefaultSlidingExpiration = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Maximum number of concurrent cache operations allowed
        /// </summary>
        private const int MaxConcurrentOperations = 100;

        public ApiCacheService(IMemoryCache memoryCache, ILogger<ApiCacheService> logger)
        {
            _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _statistics = new CacheStatistics
            {
                GeneratedAt = DateTime.UtcNow
            };
            _semaphore = new SemaphoreSlim(MaxConcurrentOperations, MaxConcurrentOperations);
            _keyTracker = new Dictionary<string, object>();

            _logger.LogInformation("ApiCacheService initialized with default expiration policies");
        }

        /// <summary>
        /// Retrieves a cached value by its unique key
        /// </summary>
        public async Task<ApiResponse<T>> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        {
            try
            {
                ValidateKey(key);
                
                await _semaphore.WaitAsync(cancellationToken);
                try
                {
                    if (_memoryCache.TryGetValue(key, out var cachedValue))
                    {
                        IncrementHitCount();
                        
                        if (cachedValue is T typedValue)
                        {
                            _logger.LogDebug("Cache hit for key: {Key}", key);
                            return ApiResponse<T>.Ok(typedValue);
                        }
                        
                        _logger.LogWarning("Cache hit but type mismatch for key: {Key}. Expected: {ExpectedType}, Actual: {ActualType}", 
                            key, typeof(T).Name, cachedValue?.GetType().Name ?? "null");
                        
                        // Remove invalid entry
                        _memoryCache.Remove(key);
                        RemoveFromKeyTracker(key);
                    }

                    IncrementMissCount();
                    _logger.LogDebug("Cache miss for key: {Key}", key);
                    return ApiResponse<T>.Fail("Cache miss - item not found or expired", ErrorCodes.NOT_FOUND);
                }
                finally
                {
                    _semaphore.Release();
                }
            }
            catch (OperationCanceledException)
            {
                return ApiResponse<T>.Fail(ErrorCodes.OPERATION_CANCELLED_MESSAGE, ErrorCodes.OPERATION_CANCELLED);
            }
            catch (ArgumentException ex)
            {
                _logger.LogError(ex, "Invalid key format: {Key}", key);
                return ApiResponse<T>.Fail($"Invalid key format: {ex.Message}", ErrorCodes.BAD_REQUEST);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving cached item with key: {Key}", key);
                return ApiResponse<T>.Fail($"Cache retrieval failed: {ex.Message}", ErrorCodes.INTERNAL_SERVER_ERROR);
            }
        }

        /// <summary>
        /// Stores a value in the cache with the specified key and optional expiration
        /// </summary>
        public async Task<ApiResponse> SetAsync<T>(
            string key, 
            T value, 
            DateTime? absoluteExpiry = null, 
            TimeSpan? slidingExpiry = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                ValidateKey(key);
                ValidateExpirationParameters(absoluteExpiry, slidingExpiry);
                
                await _semaphore.WaitAsync(cancellationToken);
                try
                {
                    var cacheEntryOptions = CreateCacheEntryOptions(absoluteExpiry, slidingExpiry);
                    
                    // Add eviction callback to track statistics
                    cacheEntryOptions.RegisterPostEvictionCallback(OnCacheItemEvicted);
                    
                    _memoryCache.Set(key, value, cacheEntryOptions);
                    AddToKeyTracker(key);
                    
                    _logger.LogDebug("Cached item with key: {Key}, Type: {Type}", key, typeof(T).Name);
                    return ApiResponse.Ok();
                }
                finally
                {
                    _semaphore.Release();
                }
            }
            catch (OperationCanceledException)
            {
                return ApiResponse.Fail(ErrorCodes.OPERATION_CANCELLED_MESSAGE, ErrorCodes.OPERATION_CANCELLED);
            }
            catch (ArgumentException ex)
            {
                _logger.LogError(ex, "Invalid parameters for caching key: {Key}", key);
                return ApiResponse.Fail($"Invalid cache parameters: {ex.Message}", ErrorCodes.BAD_REQUEST);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error caching item with key: {Key}", key);
                return ApiResponse.Fail($"Cache storage failed: {ex.Message}", ErrorCodes.INTERNAL_SERVER_ERROR);
            }
        }

        /// <summary>
        /// Stores multiple key-value pairs in the cache as a batch operation
        /// </summary>
        public async Task<ApiResponse> SetBatchAsync<T>(
            IDictionary<string, T> items, 
            DateTime? absoluteExpiry = null, 
            TimeSpan? slidingExpiry = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (items == null)
                    throw new ArgumentNullException(nameof(items));
                    
                if (!items.Any())
                    throw new ArgumentException("Items dictionary cannot be empty", nameof(items));

                ValidateExpirationParameters(absoluteExpiry, slidingExpiry);
                
                await _semaphore.WaitAsync(cancellationToken);
                try
                {
                    var cacheEntryOptions = CreateCacheEntryOptions(absoluteExpiry, slidingExpiry);
                    cacheEntryOptions.RegisterPostEvictionCallback(OnCacheItemEvicted);
                    
                    var successCount = 0;
                    var errorMessages = new List<string>();
                    
                    foreach (var kvp in items)
                    {
                        try
                        {
                            ValidateKey(kvp.Key);
                            _memoryCache.Set(kvp.Key, kvp.Value, cacheEntryOptions);
                            AddToKeyTracker(kvp.Key);
                            successCount++;
                        }
                        catch (Exception ex)
                        {
                            errorMessages.Add($"Failed to cache key '{kvp.Key}': {ex.Message}");
                            _logger.LogWarning(ex, "Failed to cache item in batch operation: {Key}", kvp.Key);
                        }
                    }
                    
                    if (errorMessages.Any())
                    {
                        var errorMessage = $"Batch operation partially failed. {successCount}/{items.Count} items cached. Errors: {string.Join("; ", errorMessages)}";
                        _logger.LogWarning("Batch cache operation had {ErrorCount} errors out of {TotalCount} items", errorMessages.Count, items.Count);
                        return ApiResponse.Fail(errorMessage, ErrorCodes.UNPROCESSABLE_ENTITY);
                    }
                    
                    _logger.LogDebug("Successfully cached {Count} items in batch operation", items.Count);
                    return ApiResponse.Ok();
                }
                finally
                {
                    _semaphore.Release();
                }
            }
            catch (OperationCanceledException)
            {
                return ApiResponse.Fail(ErrorCodes.OPERATION_CANCELLED_MESSAGE, ErrorCodes.OPERATION_CANCELLED);
            }
            catch (ArgumentNullException ex)
            {
                return ApiResponse.Fail($"Parameter cannot be null: {ex.ParamName}", ErrorCodes.BAD_REQUEST);
            }
            catch (ArgumentException ex)
            {
                return ApiResponse.Fail($"Invalid parameters: {ex.Message}", ErrorCodes.BAD_REQUEST);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in batch cache operation");
                return ApiResponse.Fail($"Batch cache operation failed: {ex.Message}", ErrorCodes.INTERNAL_SERVER_ERROR);
            }
        }

        /// <summary>
        /// Removes a cached item by its unique key
        /// </summary>
        public async Task<ApiResponse> RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            try
            {
                ValidateKey(key);
                
                await _semaphore.WaitAsync(cancellationToken);
                try
                {
                    _memoryCache.Remove(key);
                    RemoveFromKeyTracker(key);
                    
                    _logger.LogDebug("Removed cached item with key: {Key}", key);
                    return ApiResponse.Ok();
                }
                finally
                {
                    _semaphore.Release();
                }
            }
            catch (OperationCanceledException)
            {
                return ApiResponse.Fail(ErrorCodes.OPERATION_CANCELLED_MESSAGE, ErrorCodes.OPERATION_CANCELLED);
            }
            catch (ArgumentException ex)
            {
                _logger.LogError(ex, "Invalid key format: {Key}", key);
                return ApiResponse.Fail($"Invalid key format: {ex.Message}", ErrorCodes.BAD_REQUEST);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing cached item with key: {Key}", key);
                return ApiResponse.Fail($"Cache removal failed: {ex.Message}", ErrorCodes.INTERNAL_SERVER_ERROR);
            }
        }

        /// <summary>
        /// Removes multiple cached items by their keys in a single batch operation
        /// </summary>
        public async Task<ApiResponse> RemoveBatchAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default)
        {
            try
            {
                if (keys == null)
                    throw new ArgumentNullException(nameof(keys));
                    
                var keysList = keys.ToList();
                if (!keysList.Any())
                    throw new ArgumentException("Keys collection cannot be empty", nameof(keys));
                
                await _semaphore.WaitAsync(cancellationToken);
                try
                {
                    var removedCount = 0;
                    var errorMessages = new List<string>();
                    
                    foreach (var key in keysList)
                    {
                        try
                        {
                            ValidateKey(key);
                            _memoryCache.Remove(key);
                            RemoveFromKeyTracker(key);
                            removedCount++;
                        }
                        catch (Exception ex)
                        {
                            errorMessages.Add($"Failed to remove key '{key}': {ex.Message}");
                            _logger.LogWarning(ex, "Failed to remove item in batch operation: {Key}", key);
                        }
                    }
                    
                    if (errorMessages.Any())
                    {
                        var errorMessage = $"Batch removal partially failed. {removedCount}/{keysList.Count} items removed. Errors: {string.Join("; ", errorMessages)}";
                        _logger.LogWarning("Batch removal operation had {ErrorCount} errors out of {TotalCount} items", errorMessages.Count, keysList.Count);
                        return ApiResponse.Fail(errorMessage, ErrorCodes.UNPROCESSABLE_ENTITY);
                    }
                    
                    _logger.LogDebug("Successfully removed {Count} items in batch operation", keysList.Count);
                    return ApiResponse.Ok();
                }
                finally
                {
                    _semaphore.Release();
                }
            }
            catch (OperationCanceledException)
            {
                return ApiResponse.Fail(ErrorCodes.OPERATION_CANCELLED_MESSAGE, ErrorCodes.OPERATION_CANCELLED);
            }
            catch (ArgumentNullException ex)
            {
                return ApiResponse.Fail($"Parameter cannot be null: {ex.ParamName}", ErrorCodes.BAD_REQUEST);
            }
            catch (ArgumentException ex)
            {
                return ApiResponse.Fail($"Invalid parameters: {ex.Message}", ErrorCodes.BAD_REQUEST);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in batch removal operation");
                return ApiResponse.Fail($"Batch removal failed: {ex.Message}", ErrorCodes.INTERNAL_SERVER_ERROR);
            }
        }

        /// <summary>
        /// Checks whether a cached item exists and is not expired
        /// </summary>
        public async Task<ApiResponse<bool>> ExistsAsync(string key, CancellationToken cancellationToken = default)
        {
            try
            {
                ValidateKey(key);
                
                await _semaphore.WaitAsync(cancellationToken);
                try
                {
                    var exists = _memoryCache.TryGetValue(key, out _);
                    _logger.LogDebug("Cache existence check for key: {Key} - {Exists}", key, exists ? "Found" : "Not Found");
                    return ApiResponse<bool>.Ok(exists);
                }
                finally
                {
                    _semaphore.Release();
                }
            }
            catch (OperationCanceledException)
            {
                return ApiResponse<bool>.Fail(ErrorCodes.OPERATION_CANCELLED_MESSAGE, ErrorCodes.OPERATION_CANCELLED);
            }
            catch (ArgumentException ex)
            {
                _logger.LogError(ex, "Invalid key format: {Key}", key);
                return ApiResponse<bool>.Fail($"Invalid key format: {ex.Message}", ErrorCodes.BAD_REQUEST);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking cache existence for key: {Key}", key);
                return ApiResponse<bool>.Fail($"Cache existence check failed: {ex.Message}", ErrorCodes.INTERNAL_SERVER_ERROR);
            }
        }

        /// <summary>
        /// Retrieves all cache keys that match the specified pattern
        /// </summary>
        public async Task<ApiResponse<IEnumerable<string>>> GetKeysAsync(string pattern, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(pattern))
                    throw new ArgumentException("Pattern cannot be null or empty", nameof(pattern));
                
                await _semaphore.WaitAsync(cancellationToken);
                try
                {
                    var regex = CreateRegexFromPattern(pattern);
                    var matchingKeys = new List<string>();
                    
                    lock (_keyTrackerLock)
                    {
                        foreach (var key in _keyTracker.Keys)
                        {
                            if (regex.IsMatch(key))
                            {
                                matchingKeys.Add(key);
                            }
                        }
                    }
                    
                    _logger.LogDebug("Found {Count} keys matching pattern: {Pattern}", matchingKeys.Count, pattern);
                    return ApiResponse<IEnumerable<string>>.Ok(matchingKeys);
                }
                finally
                {
                    _semaphore.Release();
                }
            }
            catch (OperationCanceledException)
            {
                return ApiResponse<IEnumerable<string>>.Fail(ErrorCodes.OPERATION_CANCELLED_MESSAGE, ErrorCodes.OPERATION_CANCELLED);
            }
            catch (ArgumentException ex)
            {
                return ApiResponse<IEnumerable<string>>.Fail($"Invalid pattern: {ex.Message}", ErrorCodes.BAD_REQUEST);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving keys with pattern: {Pattern}", pattern);
                return ApiResponse<IEnumerable<string>>.Fail($"Key retrieval failed: {ex.Message}", ErrorCodes.INTERNAL_SERVER_ERROR);
            }
        }

        /// <summary>
        /// Removes all cached items that match the specified key pattern
        /// </summary>
        public async Task<ApiResponse<int>> RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(pattern))
                    throw new ArgumentException("Pattern cannot be null or empty", nameof(pattern));
                
                await _semaphore.WaitAsync(cancellationToken);
                try
                {
                    var regex = CreateRegexFromPattern(pattern);
                    var keysToRemove = new List<string>();
                    
                    lock (_keyTrackerLock)
                    {
                        foreach (var key in _keyTracker.Keys)
                        {
                            if (regex.IsMatch(key))
                            {
                                keysToRemove.Add(key);
                            }
                        }
                    }
                    
                    foreach (var key in keysToRemove)
                    {
                        _memoryCache.Remove(key);
                        RemoveFromKeyTracker(key);
                    }
                    
                    _logger.LogInformation("Removed {Count} cached items matching pattern: {Pattern}", keysToRemove.Count, pattern);
                    return ApiResponse<int>.Ok(keysToRemove.Count);
                }
                finally
                {
                    _semaphore.Release();
                }
            }
            catch (OperationCanceledException)
            {
                return ApiResponse<int>.Fail(ErrorCodes.OPERATION_CANCELLED_MESSAGE, ErrorCodes.OPERATION_CANCELLED);
            }
            catch (ArgumentException ex)
            {
                return ApiResponse<int>.Fail($"Invalid pattern: {ex.Message}", ErrorCodes.BAD_REQUEST);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing items by pattern: {Pattern}", pattern);
                return ApiResponse<int>.Fail($"Pattern removal failed: {ex.Message}", ErrorCodes.INTERNAL_SERVER_ERROR);
            }
        }

        /// <summary>
        /// Clears all items from the cache
        /// </summary>
        public async Task<ApiResponse> ClearAllAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await _semaphore.WaitAsync(cancellationToken);
                try
                {
                    var keysToRemove = new List<string>();
                    
                    lock (_keyTrackerLock)
                    {
                        keysToRemove.AddRange(_keyTracker.Keys);
                    }
                    
                    foreach (var key in keysToRemove)
                    {
                        _memoryCache.Remove(key);
                    }
                    
                    lock (_keyTrackerLock)
                    {
                        _keyTracker.Clear();
                    }
                    
                    // Reset statistics
                    lock (_statisticsLock)
                    {
                        _statistics.HitCount = 0;
                        _statistics.MissCount = 0;
                        _statistics.ExpiredItemCount = 0;
                        _statistics.EvictedItemCount = 0;
                        _statistics.LastResetAt = DateTime.UtcNow;
                        _statistics.GeneratedAt = DateTime.UtcNow;
                    }
                    
                    _logger.LogWarning("Cleared all {Count} items from cache", keysToRemove.Count);
                    return ApiResponse.Ok();
                }
                finally
                {
                    _semaphore.Release();
                }
            }
            catch (OperationCanceledException)
            {
                return ApiResponse.Fail(ErrorCodes.OPERATION_CANCELLED_MESSAGE, ErrorCodes.OPERATION_CANCELLED);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing all cache items");
                return ApiResponse.Fail($"Cache clear failed: {ex.Message}", ErrorCodes.INTERNAL_SERVER_ERROR);
            }
        }

        /// <summary>
        /// Retrieves comprehensive cache statistics and performance metrics
        /// </summary>
        public async Task<ApiResponse<CacheStatistics>> GetStatisticsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await _semaphore.WaitAsync(cancellationToken);
                try
                {
                    lock (_statisticsLock)
                    {
                        lock (_keyTrackerLock)
                        {
                            _statistics.ItemCount = _keyTracker.Count;
                        }
                        
                        _statistics.GeneratedAt = DateTime.UtcNow;
                        
                        // Create a copy to avoid thread safety issues
                        var statisticsCopy = new CacheStatistics
                        {
                            ItemCount = _statistics.ItemCount,
                            HitCount = _statistics.HitCount,
                            MissCount = _statistics.MissCount,
                            ExpiredItemCount = _statistics.ExpiredItemCount,
                            EvictedItemCount = _statistics.EvictedItemCount,
                            GeneratedAt = _statistics.GeneratedAt,
                            LastResetAt = _statistics.LastResetAt
                        };
                        
                        return ApiResponse<CacheStatistics>.Ok(statisticsCopy);
                    }
                }
                finally
                {
                    _semaphore.Release();
                }
            }
            catch (OperationCanceledException)
            {
                return ApiResponse<CacheStatistics>.Fail(ErrorCodes.OPERATION_CANCELLED_MESSAGE, ErrorCodes.OPERATION_CANCELLED);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving cache statistics");
                return ApiResponse<CacheStatistics>.Fail($"Statistics retrieval failed: {ex.Message}", ErrorCodes.INTERNAL_SERVER_ERROR);
            }
        }

        #region Cache Warming and Key Generation

        /// <summary>
        /// Generates a standardized cache key for player data
        /// </summary>
        public static string GeneratePlayerCacheKey(long contentId)
        {
            return $"player:{contentId}";
        }

        /// <summary>
        /// Generates a standardized cache key for player search results
        /// </summary>
        public static string GeneratePlayerSearchCacheKey(string name, int cursor, string worldIds)
        {
            var key = $"playersearch:{name?.ToLowerInvariant() ?? "all"}:cursor:{cursor}";
            if (!string.IsNullOrWhiteSpace(worldIds))
            {
                key += $":worlds:{worldIds}";
            }
            return key;
        }

        /// <summary>
        /// Generates a standardized cache key for server statistics
        /// </summary>
        public static string GenerateServerStatsCacheKey()
        {
            return "serverstats:current";
        }

        /// <summary>
        /// Generates a standardized cache key for user profile data
        /// </summary>
        public static string GenerateUserCacheKey(long userId)
        {
            return $"user:{userId}";
        }

        /// <summary>
        /// Generates a standardized cache key for user authentication data
        /// </summary>
        public static string GenerateUserAuthCacheKey(string username)
        {
            return $"userauth:{username?.ToLowerInvariant() ?? "anonymous"}";
        }

        /// <summary>
        /// Warms the cache with frequently accessed data
        /// </summary>
        public async Task<ApiResponse> WarmCacheAsync(Func<Task<Dictionary<string, object>>> dataProvider, CancellationToken cancellationToken = default)
        {
            try
            {
                if (dataProvider == null)
                    throw new ArgumentNullException(nameof(dataProvider));
                
                var warmupData = await dataProvider();
                if (warmupData?.Any() == true)
                {
                    var batchResult = await SetBatchAsync(warmupData, 
                        DateTime.UtcNow.Add(DefaultAbsoluteExpiration), 
                        DefaultSlidingExpiration, 
                        cancellationToken);
                    
                    if (batchResult.Success)
                    {
                        _logger.LogInformation("Cache warmed with {Count} items", warmupData.Count);
                    }
                    
                    return batchResult;
                }
                
                _logger.LogInformation("No data provided for cache warming");
                return ApiResponse.Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error warming cache");
                return ApiResponse.Fail($"Cache warming failed: {ex.Message}", ErrorCodes.INTERNAL_SERVER_ERROR);
            }
        }

        #endregion

        #region Private Helper Methods

        private static void ValidateKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Key cannot be null or empty", nameof(key));
                
            if (key.Length > 250)
                throw new ArgumentException("Key cannot be longer than 250 characters", nameof(key));
                
            if (key.Contains('\0') || key.Contains('\n') || key.Contains('\r'))
                throw new ArgumentException("Key cannot contain null, newline, or carriage return characters", nameof(key));
        }

        private static void ValidateExpirationParameters(DateTime? absoluteExpiry, TimeSpan? slidingExpiry)
        {
            if (absoluteExpiry.HasValue && absoluteExpiry.Value <= DateTime.UtcNow)
                throw new ArgumentException("Absolute expiry cannot be in the past", nameof(absoluteExpiry));
                
            if (slidingExpiry.HasValue && slidingExpiry.Value <= TimeSpan.Zero)
                throw new ArgumentException("Sliding expiry must be greater than zero", nameof(slidingExpiry));
        }

        private static MemoryCacheEntryOptions CreateCacheEntryOptions(DateTime? absoluteExpiry, TimeSpan? slidingExpiry)
        {
            var options = new MemoryCacheEntryOptions
            {
                Priority = CacheItemPriority.Normal
            };

            if (absoluteExpiry.HasValue)
            {
                options.AbsoluteExpiration = absoluteExpiry.Value;
            }
            else
            {
                options.AbsoluteExpirationRelativeToNow = DefaultAbsoluteExpiration;
            }

            if (slidingExpiry.HasValue)
            {
                options.SlidingExpiration = slidingExpiry.Value;
            }
            else
            {
                options.SlidingExpiration = DefaultSlidingExpiration;
            }

            return options;
        }

        private void OnCacheItemEvicted(object key, object? value, EvictionReason reason, object? state)
        {
            if (key is string keyString)
            {
                RemoveFromKeyTracker(keyString);
                
                lock (_statisticsLock)
                {
                    switch (reason)
                    {
                        case EvictionReason.Expired:
                            _statistics.ExpiredItemCount++;
                            _logger.LogDebug("Cache item expired: {Key}", keyString);
                            break;
                        case EvictionReason.Capacity:
                        case EvictionReason.TokenExpired:
                            _statistics.EvictedItemCount++;
                            _logger.LogDebug("Cache item evicted: {Key}, Reason: {Reason}", keyString, reason);
                            break;
                    }
                }
            }
        }

        private void AddToKeyTracker(string key)
        {
            lock (_keyTrackerLock)
            {
                _keyTracker[key] = new object(); // We only care about the key, not the value
            }
        }

        private void RemoveFromKeyTracker(string key)
        {
            lock (_keyTrackerLock)
            {
                _keyTracker.Remove(key);
            }
        }

        private void IncrementHitCount()
        {
            lock (_statisticsLock)
            {
                _statistics.HitCount++;
            }
        }

        private void IncrementMissCount()
        {
            lock (_statisticsLock)
            {
                _statistics.MissCount++;
            }
        }

        private static Regex CreateRegexFromPattern(string pattern)
        {
            // Convert wildcard pattern to regex pattern
            var regexPattern = "^" + Regex.Escape(pattern)
                .Replace("\\*", ".*")
                .Replace("\\?", ".") + "$";
                
            return new Regex(regexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
        }

        #endregion

        #region IDisposable Implementation

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                _semaphore?.Dispose();
                _logger.LogInformation("ApiCacheService disposed");
                _disposed = true;
            }
        }

        #endregion
    }
}