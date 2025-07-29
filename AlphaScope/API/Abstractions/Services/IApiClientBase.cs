using Microsoft.Extensions.Logging;
using RestSharp;
using System;
using System.Threading;
using System.Threading.Tasks;
using AlphaScope.API.Models.Shared;

namespace AlphaScope.API.Abstractions.Services
{
    /// <summary>
    /// Base interface for API service classes providing shared functionality and common dependencies.
    /// Establishes the foundation for all API service implementations with standardized access to 
    /// HTTP client capabilities, configuration management, and logging infrastructure.
    /// </summary>
    public interface IApiClientBase : IDisposable
    {
        /// <summary>
        /// RestSharp HTTP client instance for making API requests.
        /// Provides access to the configured HTTP client with base URL, serialization settings,
        /// and authentication handlers pre-configured for the AlphaScope API.
        /// </summary>
        IRestClient RestClient { get; }
        
        /// <summary>
        /// Plugin configuration instance containing API settings and user preferences.
        /// Provides access to base URL, API keys, language settings, and other configuration options
        /// required for API operations and user customization.
        /// </summary>
        Configuration Config { get; }
        
        /// <summary>
        /// Logger instance for recording API operations, errors, and diagnostic information.
        /// Supports structured logging with appropriate log levels for debugging, monitoring,
        /// and troubleshooting API interactions.
        /// </summary>
        ILogger Logger { get; }

        /// <summary>
        /// Validates that the service is properly configured and ready for API operations.
        /// Checks essential configuration values, network connectivity prerequisites,
        /// and authentication status to ensure reliable API communication.
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token to cancel the validation</param>
        /// <returns>
        /// A task that represents the asynchronous operation.
        /// The task result contains an ApiResponse indicating configuration validity with detailed error information.
        /// </returns>
        /// <exception cref="System.InvalidOperationException">Thrown when the service is in an invalid state</exception>
        Task<ApiResponse<bool>> ValidateConfigurationAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Performs a lightweight health check to verify API connectivity and responsiveness.
        /// Tests basic communication with the API endpoint without performing complex operations.
        /// Useful for monitoring and troubleshooting connectivity issues.
        /// </summary>
        /// <param name="timeoutMs">Timeout in milliseconds for the health check (default: 10000ms)</param>
        /// <param name="cancellationToken">Optional cancellation token to cancel the health check</param>
        /// <returns>
        /// A task that represents the asynchronous operation.
        /// The task result contains an ApiResponse with health status and response time metrics.
        /// </returns>
        /// <exception cref="System.ArgumentException">Thrown when timeoutMs is less than or equal to 0</exception>
        Task<ApiResponse<(bool IsHealthy, long ResponseTimeMs)>> PerformHealthCheckAsync(
            int timeoutMs = 10000, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves the current API client version and compatibility information.
        /// Provides version details for API compatibility checking and debugging purposes.
        /// </summary>
        /// <returns>
        /// A task that represents the asynchronous operation.
        /// The task result contains an ApiResponse with version information and compatibility status.
        /// </returns>
        Task<ApiResponse<ApiVersionInfo>> GetVersionInfoAsync();

        /// <summary>
        /// Updates the service configuration with new settings.
        /// Allows dynamic reconfiguration of API settings without requiring service restart.
        /// Changes are validated before being applied to ensure service stability.
        /// </summary>
        /// <param name="newConfig">Updated configuration settings to apply</param>
        /// <param name="cancellationToken">Optional cancellation token to cancel the update</param>
        /// <returns>
        /// A task that represents the asynchronous operation.
        /// The task result contains an ApiResponse indicating whether the configuration update was successful.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">Thrown when newConfig is null</exception>
        /// <exception cref="System.ArgumentException">Thrown when newConfig contains invalid settings</exception>
        Task<ApiResponse> UpdateConfigurationAsync(Configuration newConfig, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves comprehensive service diagnostics and operational metrics.
        /// Provides detailed information about service health, performance, and current operational status.
        /// Useful for monitoring, troubleshooting, and performance optimization.
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token to cancel the diagnostics collection</param>
        /// <returns>
        /// A task that represents the asynchronous operation.
        /// The task result contains an ApiResponse with detailed service diagnostic information.
        /// </returns>
        Task<ApiResponse<ServiceDiagnostics>> GetServiceDiagnosticsAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Data structure containing API version and compatibility information.
    /// Provides details about the current client version and server compatibility status.
    /// </summary>
    public class ApiVersionInfo
    {
        /// <summary>
        /// Current client version identifier.
        /// </summary>
        public string ClientVersion { get; set; } = string.Empty;

        /// <summary>
        /// Minimum supported server API version.
        /// </summary>
        public string? MinimumServerVersion { get; set; }

        /// <summary>
        /// Maximum supported server API version.
        /// </summary>
        public string? MaximumServerVersion { get; set; }

        /// <summary>
        /// Indicates whether the current client is compatible with the target server.
        /// </summary>
        public bool IsCompatible { get; set; }

        /// <summary>
        /// Additional compatibility notes or warnings.
        /// </summary>
        public string? CompatibilityNotes { get; set; }

        /// <summary>
        /// Timestamp when version information was retrieved.
        /// </summary>
        public DateTime RetrievedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Comprehensive service diagnostic information for monitoring and troubleshooting.
    /// Contains operational metrics, health indicators, and performance data.
    /// </summary>
    public class ServiceDiagnostics
    {
        /// <summary>
        /// Overall service health status.
        /// </summary>
        public bool IsHealthy { get; set; }

        /// <summary>
        /// Current service operational status.
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Time when the service was last initialized or restarted.
        /// </summary>
        public DateTime ServiceStartTime { get; set; }

        /// <summary>
        /// Duration the service has been running.
        /// </summary>
        public TimeSpan Uptime => DateTime.UtcNow - ServiceStartTime;

        /// <summary>
        /// Number of successful API requests made by this service instance.
        /// </summary>
        public long SuccessfulRequestCount { get; set; }

        /// <summary>
        /// Number of failed API requests made by this service instance.
        /// </summary>
        public long FailedRequestCount { get; set; }

        /// <summary>
        /// Average response time for API requests in milliseconds.
        /// </summary>
        public double AverageResponseTimeMs { get; set; }

        /// <summary>
        /// Last known error message, if any.
        /// </summary>
        public string? LastError { get; set; }

        /// <summary>
        /// Timestamp when the last error occurred.
        /// </summary>
        public DateTime? LastErrorAt { get; set; }

        /// <summary>
        /// Current configuration status and validity.
        /// </summary>
        public bool ConfigurationValid { get; set; }

        /// <summary>
        /// Timestamp when diagnostics were generated.
        /// </summary>
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    }
}