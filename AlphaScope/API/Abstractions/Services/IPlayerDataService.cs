using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AlphaScope.API.Models.Responses.Common;
using AlphaScope.API.Models.Responses.Player;
using AlphaScope.API.Models.Requests.Player;
using AlphaScope.API.Models.Shared;
using AlphaScope.API.Query.Player;

namespace AlphaScope.API.Abstractions.Services
{
    /// <summary>
    /// Interface for managing player data operations with the AlphaScope API.
    /// Provides methods for searching, retrieving, and uploading player data.
    /// </summary>
    public interface IPlayerDataService
    {
        /// <summary>
        /// Searches for players using the provided query parameters with pagination support.
        /// Supports filtering by world, name matching, content ID lookup, and other criteria.
        /// </summary>
        /// <typeparam name="T">The type of player data to return (PlayerDto or PlayerSearchDto)</typeparam>
        /// <param name="query">Query object containing search parameters and filters</param>
        /// <param name="cancellationToken">Optional cancellation token to cancel the operation</param>
        /// <returns>
        /// A task that represents the asynchronous operation. 
        /// The task result contains an ApiResponse with paginated player data and metadata.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">Thrown when query is null</exception>
        /// <exception cref="System.InvalidOperationException">Thrown when the service is not properly configured</exception>
        Task<ApiResponse<PaginationBase<T>>> GetPlayersAsync<T>(
            PlayerQueryObject query, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves detailed information for a specific player by their Content ID.
        /// Returns comprehensive player data including customization, job information, and metadata.
        /// </summary>
        /// <param name="contentId">The player's unique Content ID (LocalContentId)</param>
        /// <param name="cancellationToken">Optional cancellation token to cancel the operation</param>
        /// <returns>
        /// A task that represents the asynchronous operation.
        /// The task result contains an ApiResponse with detailed player information.
        /// </returns>
        /// <exception cref="System.ArgumentException">Thrown when contentId is invalid (less than or equal to 0)</exception>
        /// <exception cref="System.InvalidOperationException">Thrown when the service is not properly configured</exception>
        Task<ApiResponse<PlayerDetailed>> GetPlayerByIdAsync(
            long contentId, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Uploads a batch of player data to the server for persistence and aggregation.
        /// Used by the persistence system to sync locally collected player information.
        /// This method provides basic success/failure status without detailed error information.
        /// </summary>
        /// <param name="players">Collection of player data to upload to the server</param>
        /// <param name="cancellationToken">Optional cancellation token to cancel the operation</param>
        /// <returns>
        /// A task that represents the asynchronous operation.
        /// The task result contains an ApiResponse indicating upload success or failure.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">Thrown when players collection is null</exception>
        /// <exception cref="System.ArgumentException">Thrown when players collection is empty</exception>
        /// <exception cref="System.InvalidOperationException">Thrown when the service is not properly configured</exception>
        Task<ApiResponse> PostPlayersAsync(
            IEnumerable<PostPlayerRequest> players, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Uploads a batch of player data to the server with detailed error information and authentication status.
        /// Used by the persistence system to sync locally collected player information and detect authentication failures.
        /// Provides more comprehensive error reporting than the basic PostPlayersAsync method.
        /// </summary>
        /// <param name="players">Collection of player data to upload to the server</param>
        /// <param name="cancellationToken">Optional cancellation token to cancel the operation</param>
        /// <returns>
        /// A task that represents the asynchronous operation.
        /// The task result contains an ApiResponse with detailed upload status including authentication failure detection.
        /// The response data contains a tuple with Success and AuthenticationFailure flags.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">Thrown when players collection is null</exception>
        /// <exception cref="System.ArgumentException">Thrown when players collection is empty</exception>
        /// <exception cref="System.InvalidOperationException">Thrown when the service is not properly configured</exception>
        Task<ApiResponse<(bool Success, bool AuthenticationFailure)>> PostPlayersWithDetailsAsync(
            IEnumerable<PostPlayerRequest> players, 
            CancellationToken cancellationToken = default);
    }
}