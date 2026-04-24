using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Memoria.API.Models.Shared;

namespace Memoria.API.Abstractions.Cache
{
    /// <summary>
    /// Interface for caching abstraction providing efficient data storage and retrieval operations.
    /// Supports various caching strategies including time-based expiration, dependency-based invalidation,
    /// and memory-efficient bulk operations with comprehensive monitoring and management capabilities.
    /// </summary>
    public interface IApiCacheService
    {
        /// <summary>
        /// Retrieves a cached value by its unique key.
        /// Returns the cached data if present and not expired, otherwise returns a failed response.
        /// </summary>
        /// <typeparam name="T">The type of the cached value</typeparam>
        /// <param name="key">Unique identifier for the cached item</param>
        /// <param name="cancellationToken">Optional cancellation token to cancel the operation</param>
        /// <returns>
        /// A task that represents the asynchronous operation.
        /// The task result contains an ApiResponse with the cached value if present and valid.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">Thrown when key is null or empty</exception>
        /// <exception cref="System.ArgumentException">Thrown when key contains invalid characters</exception>
        Task<ApiResponse<T>> GetAsync<T>(string key, CancellationToken cancellationToken = default);

        /// <summary>
        /// Stores a value in the cache with the specified key and optional expiration.
        /// Supports both absolute and sliding expiration strategies for flexible cache management.
        /// </summary>
        /// <typeparam name="T">The type of the value to cache</typeparam>
        /// <param name="key">Unique identifier for the cached item</param>
        /// <param name="value">The value to store in the cache</param>
        /// <param name="absoluteExpiry">Optional absolute expiration time (UTC)</param>
        /// <param name="slidingExpiry">Optional sliding expiration duration</param>
        /// <param name="cancellationToken">Optional cancellation token to cancel the operation</param>
        /// <returns>
        /// A task that represents the asynchronous operation.
        /// The task result contains an ApiResponse indicating whether the caching operation was successful.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">Thrown when key is null or empty</exception>
        /// <exception cref="System.ArgumentException">Thrown when key contains invalid characters or expiry values are invalid</exception>
        Task<ApiResponse> SetAsync<T>(
            string key, 
            T value, 
            DateTime? absoluteExpiry = null, 
            TimeSpan? slidingExpiry = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Stores multiple key-value pairs in the cache as a batch operation.
        /// Provides efficient bulk caching with consistent expiration policies across all items.
        /// All items in the batch will share the same expiration configuration.
        /// </summary>
        /// <typeparam name="T">The type of the values to cache</typeparam>
        /// <param name="items">Dictionary of key-value pairs to cache</param>
        /// <param name="absoluteExpiry">Optional absolute expiration time (UTC) for all items</param>
        /// <param name="slidingExpiry">Optional sliding expiration duration for all items</param>
        /// <param name="cancellationToken">Optional cancellation token to cancel the operation</param>
        /// <returns>
        /// A task that represents the asynchronous operation.
        /// The task result contains an ApiResponse indicating the success of the batch operation.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">Thrown when items dictionary is null</exception>
        /// <exception cref="System.ArgumentException">Thrown when items dictionary is empty or contains invalid keys</exception>
        Task<ApiResponse> SetBatchAsync<T>(
            IDictionary<string, T> items, 
            DateTime? absoluteExpiry = null, 
            TimeSpan? slidingExpiry = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes a cached item by its unique key.
        /// Returns success regardless of whether the key existed in the cache.
        /// </summary>
        /// <param name="key">Unique identifier of the cached item to remove</param>
        /// <param name="cancellationToken">Optional cancellation token to cancel the operation</param>
        /// <returns>
        /// A task that represents the asynchronous operation.
        /// The task result contains an ApiResponse indicating whether the removal operation completed successfully.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">Thrown when key is null or empty</exception>
        /// <exception cref="System.ArgumentException">Thrown when key contains invalid characters</exception>
        Task<ApiResponse> RemoveAsync(string key, CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes multiple cached items by their keys in a single batch operation.
        /// Provides efficient bulk removal with atomic operation semantics where possible.
        /// </summary>
        /// <param name="keys">Collection of unique identifiers for the cached items to remove</param>
        /// <param name="cancellationToken">Optional cancellation token to cancel the operation</param>
        /// <returns>
        /// A task that represents the asynchronous operation.
        /// The task result contains an ApiResponse indicating the success of the batch removal operation.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">Thrown when keys collection is null</exception>
        /// <exception cref="System.ArgumentException">Thrown when keys collection is empty or contains invalid keys</exception>
        Task<ApiResponse> RemoveBatchAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks whether a cached item exists and is not expired.
        /// Provides an efficient way to verify cache presence without retrieving the actual value.
        /// </summary>
        /// <param name="key">Unique identifier of the cached item to check</param>
        /// <param name="cancellationToken">Optional cancellation token to cancel the operation</param>
        /// <returns>
        /// A task that represents the asynchronous operation.
        /// The task result contains an ApiResponse with a boolean indicating cache item existence.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">Thrown when key is null or empty</exception>
        /// <exception cref="System.ArgumentException">Thrown when key contains invalid characters</exception>
        Task<ApiResponse<bool>> ExistsAsync(string key, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves all cache keys that match the specified pattern.
        /// Supports wildcard patterns for flexible key discovery and cache management operations.
        /// Use with caution in high-volume caching scenarios as this operation can be expensive.
        /// </summary>
        /// <param name="pattern">Pattern to match against cache keys (supports wildcards like * and ?)</param>
        /// <param name="cancellationToken">Optional cancellation token to cancel the operation</param>
        /// <returns>
        /// A task that represents the asynchronous operation.
        /// The task result contains an ApiResponse with a collection of matching cache keys.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">Thrown when pattern is null or empty</exception>
        /// <exception cref="System.ArgumentException">Thrown when pattern contains invalid characters</exception>
        Task<ApiResponse<IEnumerable<string>>> GetKeysAsync(string pattern, CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes all cached items that match the specified key pattern.
        /// Provides efficient pattern-based cache invalidation for related data cleanup.
        /// Use with caution as this operation can affect multiple cache entries.
        /// </summary>
        /// <param name="pattern">Pattern to match against cache keys for removal (supports wildcards like * and ?)</param>
        /// <param name="cancellationToken">Optional cancellation token to cancel the operation</param>
        /// <returns>
        /// A task that represents the asynchronous operation.
        /// The task result contains an ApiResponse with the number of items removed from the cache.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">Thrown when pattern is null or empty</exception>
        /// <exception cref="System.ArgumentException">Thrown when pattern contains invalid characters</exception>
        Task<ApiResponse<int>> RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default);

        /// <summary>
        /// Clears all items from the cache.
        /// Use with extreme caution as this operation will remove all cached data and cannot be undone.
        /// Typically used for testing scenarios or complete cache reset operations.
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token to cancel the operation</param>
        /// <returns>
        /// A task that represents the asynchronous operation.
        /// The task result contains an ApiResponse indicating whether the clear operation was successful.
        /// </returns>
        Task<ApiResponse> ClearAllAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves comprehensive cache statistics and performance metrics.
        /// Provides insights into cache hit rates, memory usage, item counts, and other operational data.
        /// Useful for monitoring cache performance and optimizing caching strategies.
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token to cancel the operation</param>
        /// <returns>
        /// A task that represents the asynchronous operation.
        /// The task result contains an ApiResponse with detailed cache statistics.
        /// </returns>
        Task<ApiResponse<CacheStatistics>> GetStatisticsAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Data structure containing comprehensive cache performance and usage statistics.
    /// Provides detailed insights into cache behavior for monitoring and optimization purposes.
    /// </summary>
    public class CacheStatistics
    {
        /// <summary>
        /// Total number of items currently stored in the cache.
        /// </summary>
        public long ItemCount { get; set; }

        /// <summary>
        /// Total number of cache hit requests since last reset.
        /// </summary>
        public long HitCount { get; set; }

        /// <summary>
        /// Total number of cache miss requests since last reset.
        /// </summary>
        public long MissCount { get; set; }

        /// <summary>
        /// Cache hit rate as a percentage (0.0 to 100.0).
        /// </summary>
        public double HitRate => HitCount + MissCount > 0 ? (double)HitCount / (HitCount + MissCount) * 100 : 0.0;

        /// <summary>
        /// Estimated memory usage of the cache in bytes.
        /// May not be available on all cache implementations.
        /// </summary>
        public long? MemoryUsageBytes { get; set; }

        /// <summary>
        /// Number of items that have expired and been removed from the cache.
        /// </summary>
        public long ExpiredItemCount { get; set; }

        /// <summary>
        /// Number of items that have been evicted due to memory pressure or other policies.
        /// </summary>
        public long EvictedItemCount { get; set; }

        /// <summary>
        /// Timestamp when these statistics were generated.
        /// </summary>
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Time when the cache statistics were last reset.
        /// </summary>
        public DateTime? LastResetAt { get; set; }
    }
}