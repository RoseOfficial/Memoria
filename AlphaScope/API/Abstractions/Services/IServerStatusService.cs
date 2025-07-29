using System.Threading;
using System.Threading.Tasks;
using AlphaScope.API.Models.Responses.Server;
using AlphaScope.API.Models.Shared;

namespace AlphaScope.API.Abstractions.Services
{
    /// <summary>
    /// Interface for managing server status checks and statistics retrieval.
    /// Provides methods for monitoring AlphaScope server health, connectivity, and performance metrics.
    /// </summary>
    public interface IServerStatusService
    {
        /// <summary>
        /// Current server status message indicating connectivity state.
        /// Possible values include "ONLINE", error messages, or connectivity status descriptions.
        /// </summary>
        string ServerStatus { get; }
        
        /// <summary>
        /// Flag indicating whether a server status check operation is currently in progress.
        /// Used to prevent concurrent status checks and provide UI feedback.
        /// </summary>
        bool IsCheckingServerStatus { get; }
        
        /// <summary>
        /// Last measured ping response time in milliseconds from the server.
        /// Returns -1 if no ping measurement is available or the server is unreachable.
        /// </summary>
        long LastPingValue { get; }
        
        /// <summary>
        /// Cached server statistics and associated status message from the last successful request.
        /// Provides access to the most recent server metrics without requiring a new API call.
        /// </summary>
        (ServerStatsDto? ServerStats, string Message) LastServerStats { get; }

        /// <summary>
        /// Performs a comprehensive health check of the AlphaScope server.
        /// Executes both an HTTP request to verify API responsiveness and a network ping to measure latency.
        /// Updates the ServerStatus, LastPingValue, and IsCheckingServerStatus properties.
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token to cancel the operation</param>
        /// <returns>
        /// A task that represents the asynchronous operation.
        /// The task result contains an ApiResponse with a boolean indicating server availability.
        /// </returns>
        /// <exception cref="System.InvalidOperationException">Thrown when the service is not properly configured</exception>
        /// <exception cref="System.Net.NetworkInformation.PingException">Thrown when network ping operations fail</exception>
        Task<ApiResponse<bool>> CheckServerStatusAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves comprehensive server statistics and performance metrics.
        /// Includes data such as user counts, data storage metrics, system health indicators,
        /// and other operational statistics. Results are cached in LastServerStats property.
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token to cancel the operation</param>
        /// <returns>
        /// A task that represents the asynchronous operation.
        /// The task result contains an ApiResponse with detailed server statistics.
        /// </returns>
        /// <exception cref="System.InvalidOperationException">Thrown when the service is not properly configured</exception>
        /// <exception cref="System.Text.Json.JsonException">Thrown when server response cannot be parsed</exception>
        Task<ApiResponse<ServerStatsDto>> CheckServerStatsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Performs a lightweight ping test to measure network latency to the server.
        /// This method focuses solely on network connectivity without checking API functionality.
        /// </summary>
        /// <param name="timeoutMs">Timeout in milliseconds for the ping operation (default: 5000ms)</param>
        /// <param name="cancellationToken">Optional cancellation token to cancel the operation</param>
        /// <returns>
        /// A task that represents the asynchronous operation.
        /// The task result contains an ApiResponse with the ping time in milliseconds.
        /// Returns -1 in the response data if the ping fails.
        /// </returns>
        /// <exception cref="System.ArgumentException">Thrown when timeoutMs is less than or equal to 0</exception>
        /// <exception cref="System.Net.NetworkInformation.PingException">Thrown when ping operations fail</exception>
        Task<ApiResponse<long>> PingServerAsync(int timeoutMs = 5000, CancellationToken cancellationToken = default);
    }
}