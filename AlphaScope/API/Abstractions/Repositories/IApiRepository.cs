using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AlphaScope.API.Models.Shared;

namespace AlphaScope.API.Abstractions.Repositories
{
    /// <summary>
    /// Interface for data access abstraction providing generic repository operations.
    /// Encapsulates common data access patterns for API operations with proper error handling and cancellation support.
    /// Supports both single entity and batch operations with comprehensive result reporting.
    /// </summary>
    /// <typeparam name="TEntity">The type of entity this repository manages</typeparam>
    /// <typeparam name="TKey">The type of the entity's primary key</typeparam>
    public interface IApiRepository<TEntity, TKey> 
        where TEntity : class
        where TKey : notnull
    {
        /// <summary>
        /// Retrieves a single entity by its unique identifier.
        /// Provides a strongly-typed way to fetch individual records with proper error handling.
        /// </summary>
        /// <param name="id">The unique identifier of the entity to retrieve</param>
        /// <param name="cancellationToken">Optional cancellation token to cancel the operation</param>
        /// <returns>
        /// A task that represents the asynchronous operation.
        /// The task result contains an ApiResponse with the requested entity if found.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">Thrown when id is null</exception>
        /// <exception cref="System.ArgumentException">Thrown when id is in an invalid format</exception>
        Task<ApiResponse<TEntity>> GetByIdAsync(TKey id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves all entities that match the specified criteria.
        /// Supports flexible querying with optional filtering, sorting, and pagination.
        /// </summary>
        /// <param name="filter">Optional filter expression to apply to the query</param>
        /// <param name="orderBy">Optional ordering specification for the results</param>
        /// <param name="skip">Number of records to skip for pagination (default: 0)</param>
        /// <param name="take">Maximum number of records to return (default: no limit)</param>
        /// <param name="cancellationToken">Optional cancellation token to cancel the operation</param>
        /// <returns>
        /// A task that represents the asynchronous operation.
        /// The task result contains an ApiResponse with a collection of matching entities.
        /// </returns>
        /// <exception cref="System.ArgumentException">Thrown when skip is negative or take is less than or equal to 0</exception>
        Task<ApiResponse<IEnumerable<TEntity>>> GetAllAsync(
            System.Linq.Expressions.Expression<System.Func<TEntity, bool>>? filter = null,
            System.Func<System.Linq.IQueryable<TEntity>, System.Linq.IOrderedQueryable<TEntity>>? orderBy = null,
            int skip = 0,
            int? take = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a new entity in the data store.
        /// Performs validation and ensures data integrity before persistence.
        /// </summary>
        /// <param name="entity">The entity to create</param>
        /// <param name="cancellationToken">Optional cancellation token to cancel the operation</param>
        /// <returns>
        /// A task that represents the asynchronous operation.
        /// The task result contains an ApiResponse with the created entity including any generated identifiers.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">Thrown when entity is null</exception>
        /// <exception cref="System.ArgumentException">Thrown when entity validation fails</exception>
        /// <exception cref="System.InvalidOperationException">Thrown when the entity already exists</exception>
        Task<ApiResponse<TEntity>> CreateAsync(TEntity entity, CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates multiple entities in a single batch operation.
        /// Provides efficient bulk insertion with transaction support and partial failure handling.
        /// </summary>
        /// <param name="entities">Collection of entities to create</param>
        /// <param name="cancellationToken">Optional cancellation token to cancel the operation</param>
        /// <returns>
        /// A task that represents the asynchronous operation.
        /// The task result contains an ApiResponse with the created entities and operation statistics.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">Thrown when entities collection is null</exception>
        /// <exception cref="System.ArgumentException">Thrown when entities collection is empty or contains invalid data</exception>
        Task<ApiResponse<IEnumerable<TEntity>>> CreateBatchAsync(
            IEnumerable<TEntity> entities, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates an existing entity in the data store.
        /// Performs optimistic concurrency checking and validation before applying changes.
        /// </summary>
        /// <param name="entity">The entity with updated values</param>
        /// <param name="cancellationToken">Optional cancellation token to cancel the operation</param>
        /// <returns>
        /// A task that represents the asynchronous operation.
        /// The task result contains an ApiResponse with the updated entity.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">Thrown when entity is null</exception>
        /// <exception cref="System.ArgumentException">Thrown when entity validation fails</exception>
        /// <exception cref="System.InvalidOperationException">Thrown when the entity doesn't exist or has been modified by another process</exception>
        Task<ApiResponse<TEntity>> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes an entity from the data store by its unique identifier.
        /// Performs cascading delete operations and referential integrity checks as needed.
        /// </summary>
        /// <param name="id">The unique identifier of the entity to delete</param>
        /// <param name="cancellationToken">Optional cancellation token to cancel the operation</param>
        /// <returns>
        /// A task that represents the asynchronous operation.
        /// The task result contains an ApiResponse indicating whether the deletion was successful.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">Thrown when id is null</exception>
        /// <exception cref="System.ArgumentException">Thrown when id is in an invalid format</exception>
        /// <exception cref="System.InvalidOperationException">Thrown when the entity cannot be deleted due to dependencies</exception>
        Task<ApiResponse> DeleteAsync(TKey id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks whether an entity with the specified identifier exists in the data store.
        /// Provides an efficient way to verify entity existence without retrieving the full entity.
        /// </summary>
        /// <param name="id">The unique identifier to check for existence</param>
        /// <param name="cancellationToken">Optional cancellation token to cancel the operation</param>
        /// <returns>
        /// A task that represents the asynchronous operation.
        /// The task result contains an ApiResponse with a boolean indicating entity existence.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">Thrown when id is null</exception>
        /// <exception cref="System.ArgumentException">Thrown when id is in an invalid format</exception>
        Task<ApiResponse<bool>> ExistsAsync(TKey id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Counts the total number of entities that match the specified criteria.
        /// Provides efficient counting without retrieving the actual entities.
        /// </summary>
        /// <param name="filter">Optional filter expression to apply when counting</param>
        /// <param name="cancellationToken">Optional cancellation token to cancel the operation</param>
        /// <returns>
        /// A task that represents the asynchronous operation.
        /// The task result contains an ApiResponse with the count of matching entities.
        /// </returns>
        Task<ApiResponse<long>> CountAsync(
            System.Linq.Expressions.Expression<System.Func<TEntity, bool>>? filter = null,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Specialized repository interface for entities with string-based primary keys.
    /// Provides common repository operations optimized for string identifier types.
    /// </summary>
    /// <typeparam name="TEntity">The type of entity this repository manages</typeparam>
    public interface IApiRepository<TEntity> : IApiRepository<TEntity, string>
        where TEntity : class
    {
    }
}