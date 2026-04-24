using Microsoft.Extensions.Logging;
using RestSharp;
using System;
using System.Threading;
using System.Threading.Tasks;
using Memoria.API.Abstractions.Services;
using Memoria.API.Models.Shared;
using Memoria.API.Constants;
using Memoria.Properties;

namespace Memoria.API.Services
{
    /// <summary>
    /// Base class for API service implementations providing shared functionality and common IApiClientBase implementation.
    /// Eliminates code duplication across service classes by providing standard implementations of common API operations,
    /// error handling patterns, and configuration management.
    /// </summary>
    public abstract class BaseApiService : IApiClientBase
    {
        /// <summary>
        /// RestSharp HTTP client instance for making API requests
        /// </summary>
        public IRestClient RestClient { get; }
        
        /// <summary>
        /// Plugin configuration instance containing API settings and user preferences
        /// </summary>
        public Configuration Config { get; }
        
        /// <summary>
        /// Logger instance for recording API operations, errors, and diagnostic information
        /// </summary>
        public ILogger Logger { get; }

        /// <summary>
        /// Initializes a new instance of the BaseApiService class
        /// </summary>
        /// <param name="restClient">RestSharp HTTP client for API requests</param>
        /// <param name="config">Plugin configuration instance</param>
        /// <param name="logger">Logger instance for diagnostic information</param>
        /// <exception cref="ArgumentNullException">Thrown when any parameter is null</exception>
        protected BaseApiService(IRestClient restClient, Configuration config, ILogger logger)
        {
            RestClient = restClient ?? throw new ArgumentNullException(nameof(restClient));  
            Config = config ?? throw new ArgumentNullException(nameof(config));
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #region IApiClientBase Implementation

        /// <summary>
        /// Validates that the service is properly configured and ready for API operations
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token to cancel the validation</param>
        /// <returns>ApiResponse indicating configuration validity with detailed error information</returns>
        public virtual Task<ApiResponse<bool>> ValidateConfigurationAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Config.BaseUrl))
                    return Task.FromResult(ApiResponse<bool>.Fail(ErrorCodes.BASE_URL_NOT_CONFIGURED_MESSAGE, ErrorCodes.BAD_REQUEST));

                if (!Uri.IsWellFormedUriString(Config.BaseUrl, UriKind.Absolute))
                    return Task.FromResult(ApiResponse<bool>.Fail(ErrorCodes.INVALID_URL_MESSAGE, ErrorCodes.BAD_REQUEST));

                return Task.FromResult(ApiResponse<bool>.Ok(true));
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error validating configuration");
                return Task.FromResult(ApiResponse<bool>.Fail($"{ErrorCodes.CONFIGURATION_VALIDATION_FAILED_MESSAGE}: {ex.Message}", ErrorCodes.INTERNAL_SERVER_ERROR));
            }
        }

        /// <summary>
        /// Performs a lightweight health check to verify API connectivity
        /// </summary>
        /// <param name="timeoutMs">Timeout in milliseconds for the health check (default: 10000ms)</param>
        /// <param name="cancellationToken">Optional cancellation token to cancel the health check</param>
        /// <returns>ApiResponse with health status and response time metrics</returns>
        public virtual async Task<ApiResponse<(bool IsHealthy, long ResponseTimeMs)>> PerformHealthCheckAsync(int timeoutMs = 10000, CancellationToken cancellationToken = default)
        {
            try
            {
                if (timeoutMs <= 0)
                    return ApiResponse<(bool IsHealthy, long ResponseTimeMs)>.Fail(ErrorCodes.TIMEOUT_MUST_BE_GREATER_THAN_ZERO_MESSAGE, ErrorCodes.BAD_REQUEST);

                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                var request = new RestRequest(ApiEndpoints.HEALTH)
                    .AddHeader(ApiHeaders.VERSION, Utils.clientVer);

                var response = await RestClient.ExecuteGetAsync(request, cancellationToken).ConfigureAwait(false);
                stopwatch.Stop();

                var isHealthy = response.IsSuccessful;
                return ApiResponse<(bool IsHealthy, long ResponseTimeMs)>.Ok((isHealthy, stopwatch.ElapsedMilliseconds));
            }
            catch (OperationCanceledException)
            {
                return ApiResponse<(bool IsHealthy, long ResponseTimeMs)>.Fail(ErrorCodes.OPERATION_CANCELLED_MESSAGE, ErrorCodes.OPERATION_CANCELLED);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error performing health check");
                return ApiResponse<(bool IsHealthy, long ResponseTimeMs)>.Fail($"{ErrorCodes.HEALTH_CHECK_FAILED_MESSAGE}: {ex.Message}", ErrorCodes.INTERNAL_SERVER_ERROR);
            }
        }

        /// <summary>
        /// Retrieves API version information
        /// </summary>
        /// <returns>ApiResponse with version information and compatibility status</returns>
        public virtual Task<ApiResponse<ApiVersionInfo>> GetVersionInfoAsync()
        {
            try
            {
                var versionInfo = new ApiVersionInfo
                {
                    ClientVersion = Utils.clientVer,
                    IsCompatible = true,
                    RetrievedAt = DateTime.UtcNow
                };

                return Task.FromResult(ApiResponse<ApiVersionInfo>.Ok(versionInfo));
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error retrieving version info");
                return Task.FromResult(ApiResponse<ApiVersionInfo>.Fail($"{ErrorCodes.VERSION_INFO_RETRIEVAL_FAILED_MESSAGE}: {ex.Message}", ErrorCodes.INTERNAL_SERVER_ERROR));
            }
        }

        /// <summary>
        /// Updates the service configuration with new settings
        /// </summary>
        /// <param name="newConfig">Updated configuration settings to apply</param>
        /// <param name="cancellationToken">Optional cancellation token to cancel the update</param>
        /// <returns>ApiResponse indicating whether the configuration update was successful</returns>
        public virtual Task<ApiResponse> UpdateConfigurationAsync(Configuration newConfig, CancellationToken cancellationToken = default)
        {
            try
            {
                if (newConfig == null)
                    return Task.FromResult(ApiResponse.Fail(ErrorCodes.PARAMETER_CANNOT_BE_NULL_MESSAGE, ErrorCodes.BAD_REQUEST));

                // Validate new configuration
                if (string.IsNullOrWhiteSpace(newConfig.BaseUrl))
                    return Task.FromResult(ApiResponse.Fail(ErrorCodes.BASE_URL_IS_REQUIRED_MESSAGE, ErrorCodes.BAD_REQUEST));

                if (!Uri.IsWellFormedUriString(newConfig.BaseUrl, UriKind.Absolute))
                    return Task.FromResult(ApiResponse.Fail(ErrorCodes.INVALID_URL_MESSAGE, ErrorCodes.BAD_REQUEST));

                // Configuration update would need to be handled by the calling code
                // as this service doesn't own the configuration instance
                return Task.FromResult(ApiResponse.Ok());
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error updating configuration");
                return Task.FromResult(ApiResponse.Fail($"{ErrorCodes.CONFIGURATION_UPDATE_FAILED_MESSAGE}: {ex.Message}", ErrorCodes.INTERNAL_SERVER_ERROR));
            }
        }

        /// <summary>
        /// Retrieves comprehensive service diagnostics and operational metrics
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token to cancel the diagnostics collection</param>
        /// <returns>ApiResponse with detailed service diagnostic information</returns>
        public virtual Task<ApiResponse<ServiceDiagnostics>> GetServiceDiagnosticsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var diagnostics = CreateServiceDiagnostics();
                return Task.FromResult(ApiResponse<ServiceDiagnostics>.Ok(diagnostics));
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error retrieving service diagnostics");
                return Task.FromResult(ApiResponse<ServiceDiagnostics>.Fail($"{ErrorCodes.DIAGNOSTICS_RETRIEVAL_FAILED_MESSAGE}: {ex.Message}", ErrorCodes.INTERNAL_SERVER_ERROR));
            }
        }

        /// <summary>
        /// Creates basic service diagnostics. Override in derived classes to provide service-specific diagnostics.
        /// </summary>
        /// <returns>ServiceDiagnostics instance with basic health and configuration information</returns>
        protected virtual ServiceDiagnostics CreateServiceDiagnostics()
        {
            return new ServiceDiagnostics
            {
                IsHealthy = !string.IsNullOrWhiteSpace(Config.BaseUrl),
                Status = "Running",
                ServiceStartTime = DateTime.UtcNow.AddHours(-1), // Placeholder
                ConfigurationValid = !string.IsNullOrWhiteSpace(Config.BaseUrl)
            };
        }

        /// <summary>
        /// Disposes resources used by the service
        /// </summary>
        public virtual void Dispose()
        {
            // Base implementation has no resources to dispose
            // Override in derived classes if specific cleanup is needed
        }

        #endregion

        #region Protected Helper Methods

        /// <summary>
        /// Extracts a human-readable error message from a REST response.
        /// Provides consistent error messaging across all service implementations.
        /// </summary>
        /// <param name="response">The REST response to extract error information from</param>
        /// <returns>Formatted error message string</returns>
        protected static string GetErrorMessage(RestResponse response)
        {
            ArgumentNullException.ThrowIfNull(response);
            
            if (!string.IsNullOrWhiteSpace(response.Content))
                return $"{Loc.ApiError} {response.Content}";
            if (response.StatusCode == 0)
                return $"{Loc.ApiError} {Loc.ApiServiceUnavailable}";
            return $"{Loc.ApiError} {response.StatusCode}";
        }

        /// <summary>
        /// Handles common exception types and returns appropriate ApiResponse with standardized error codes.
        /// Provides consistent exception handling patterns across all service methods.
        /// </summary>
        /// <typeparam name="T">The type of data expected in successful responses</typeparam>
        /// <param name="ex">The exception to handle</param>
        /// <param name="operationName">Name of the operation that failed, used for logging context</param>
        /// <returns>ApiResponse with appropriate error code and message</returns>
        protected ApiResponse<T> HandleCommonException<T>(Exception ex, string operationName)
        {
            return ex switch
            {
                OperationCanceledException => ApiResponse<T>.Fail(ErrorCodes.OPERATION_CANCELLED_MESSAGE, ErrorCodes.OPERATION_CANCELLED),
                ArgumentNullException argEx => ApiResponse<T>.Fail($"Parameter cannot be null: {argEx.ParamName}", ErrorCodes.BAD_REQUEST),
                ArgumentException argEx => ApiResponse<T>.Fail($"Invalid argument: {argEx.Message}", ErrorCodes.BAD_REQUEST),
                _ => LogAndReturnGenericError<T>(ex, operationName)
            };
        }

        /// <summary>
        /// Handles common exception types and returns appropriate ApiResponse with standardized error codes.
        /// Non-generic version for operations that don't return data.
        /// </summary>
        /// <param name="ex">The exception to handle</param>
        /// <param name="operationName">Name of the operation that failed, used for logging context</param>
        /// <returns>ApiResponse with appropriate error code and message</returns>
        protected ApiResponse HandleCommonException(Exception ex, string operationName)
        {
            return ex switch
            {
                OperationCanceledException => ApiResponse.Fail(ErrorCodes.OPERATION_CANCELLED_MESSAGE, ErrorCodes.OPERATION_CANCELLED),
                ArgumentNullException argEx => ApiResponse.Fail($"Parameter cannot be null: {argEx.ParamName}", ErrorCodes.BAD_REQUEST),
                ArgumentException argEx => ApiResponse.Fail($"Invalid argument: {argEx.Message}", ErrorCodes.BAD_REQUEST),
                _ => LogAndReturnGenericError(ex, operationName)
            };
        }

        /// <summary>
        /// Logs an unexpected exception and returns a generic error response
        /// </summary>
        private ApiResponse<T> LogAndReturnGenericError<T>(Exception ex, string operationName)
        {
            Logger.LogError(ex, "Error while {OperationName}", operationName);
            return ApiResponse<T>.Fail($"{Loc.ApiError} {ex.Message}", 500);
        }

        /// <summary>
        /// Logs an unexpected exception and returns a generic error response (non-generic version)
        /// </summary>
        private ApiResponse LogAndReturnGenericError(Exception ex, string operationName) 
        {
            Logger.LogError(ex, "Error while {OperationName}", operationName);
            return ApiResponse.Fail($"{Loc.ApiError} {ex.Message}", 500);
        }

        #endregion
    }
}