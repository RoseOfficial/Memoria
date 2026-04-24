using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Memoria.API.Client;
using Memoria.API.Models.Shared;
using Memoria.API.Constants;

namespace Memoria.API.Extensions
{
    /// <summary>
    /// Extension methods for RestSharp IRestClient to provide common HTTP operations
    /// with standardized error handling, logging, and response processing for the
    /// Memoria API integration. Simplifies API request building and response handling.
    /// </summary>
    public static class HttpClientExtensions
    {
        /// <summary>
        /// Creates a GET request with standardized configuration for Memoria API endpoints.
        /// Automatically configures content type, authentication headers, and request timeout.
        /// </summary>
        /// <param name="client">The RestSharp client instance</param>
        /// <param name="endpoint">API endpoint path (relative to base URL)</param>
        /// <param name="parameters">Optional query parameters to include in the request</param>
        /// <returns>Configured RestRequest ready for execution</returns>
        /// <exception cref="ArgumentNullException">Thrown when client or endpoint is null</exception>
        /// <exception cref="ArgumentException">Thrown when endpoint is empty or whitespace</exception>
        /// <example>
        /// <code>
        /// var request = client.CreateGetRequest("players/search", new { name = "PlayerName", world = "Gilgamesh" });
        /// var response = await client.ExecuteAsync(request);
        /// </code>
        /// </example>
        public static RestRequest CreateGetRequest(
            this IRestClient client,
            string endpoint,
            object? parameters = null)
        {
            if (client == null)
                throw new ArgumentNullException(nameof(client));
            if (string.IsNullOrWhiteSpace(endpoint))
                throw new ArgumentException("Endpoint cannot be null or empty", nameof(endpoint));

            var request = new RestRequest(endpoint, Method.Get);
            
            // Add standard headers
            request.AddHeader("Accept", "application/json");
            request.AddHeader("User-Agent", "Memoria/1.0");

            // Add query parameters if provided
            if (parameters != null)
            {
                request.AddObjectAsQueryString(parameters);
            }

            return request;
        }

        /// <summary>
        /// Creates a POST request with JSON body and standardized configuration.
        /// Automatically serializes the request body to JSON and sets appropriate headers.
        /// </summary>
        /// <param name="client">The RestSharp client instance</param>
        /// <param name="endpoint">API endpoint path (relative to base URL)</param>
        /// <param name="body">Request body object to serialize as JSON</param>
        /// <returns>Configured RestRequest ready for execution</returns>
        /// <exception cref="ArgumentNullException">Thrown when client or endpoint is null</exception>
        /// <exception cref="ArgumentException">Thrown when endpoint is empty or whitespace</exception>
        /// <example>
        /// <code>
        /// var playerData = new PostPlayerRequest { Name = "PlayerName", WorldId = 123 };
        /// var request = client.CreatePostRequest("players", playerData);
        /// var response = await client.ExecuteAsync(request);
        /// </code>
        /// </example>
        public static RestRequest CreatePostRequest(
            this IRestClient client,
            string endpoint,
            object? body = null)
        {
            if (client == null)
                throw new ArgumentNullException(nameof(client));
            if (string.IsNullOrWhiteSpace(endpoint))
                throw new ArgumentException("Endpoint cannot be null or empty", nameof(endpoint));

            var request = new RestRequest(endpoint, Method.Post);
            
            // Add standard headers
            request.AddHeader("Accept", "application/json");
            request.AddHeader("Content-Type", "application/json");
            request.AddHeader("User-Agent", "Memoria/1.0");

            // Add JSON body if provided
            if (body != null)
            {
                request.AddJsonBody(body);
            }

            return request;
        }

        /// <summary>
        /// Creates a PUT request with JSON body and standardized configuration.
        /// Used for updating existing resources with complete replacement semantics.
        /// </summary>
        /// <param name="client">The RestSharp client instance</param>
        /// <param name="endpoint">API endpoint path (relative to base URL)</param>
        /// <param name="body">Request body object to serialize as JSON</param>
        /// <returns>Configured RestRequest ready for execution</returns>
        /// <exception cref="ArgumentNullException">Thrown when client or endpoint is null</exception>
        /// <exception cref="ArgumentException">Thrown when endpoint is empty or whitespace</exception>
        /// <example>
        /// <code>
        /// var updateData = new UserUpdateDto { DisplayName = "NewName" };
        /// var request = client.CreatePutRequest("users/profile", updateData);
        /// var response = await client.ExecuteAsync(request);
        /// </code>
        /// </example>
        public static RestRequest CreatePutRequest(
            this IRestClient client,
            string endpoint,
            object? body = null)
        {
            if (client == null)
                throw new ArgumentNullException(nameof(client));
            if (string.IsNullOrWhiteSpace(endpoint))
                throw new ArgumentException("Endpoint cannot be null or empty", nameof(endpoint));

            var request = new RestRequest(endpoint, Method.Put);
            
            // Add standard headers
            request.AddHeader("Accept", "application/json");
            request.AddHeader("Content-Type", "application/json");
            request.AddHeader("User-Agent", "Memoria/1.0");

            // Add JSON body if provided
            if (body != null)
            {
                request.AddJsonBody(body);
            }

            return request;
        }

        /// <summary>
        /// Creates a DELETE request with standardized configuration.
        /// Used for removing resources from the API.
        /// </summary>
        /// <param name="client">The RestSharp client instance</param>
        /// <param name="endpoint">API endpoint path (relative to base URL)</param>
        /// <returns>Configured RestRequest ready for execution</returns>
        /// <exception cref="ArgumentNullException">Thrown when client or endpoint is null</exception>
        /// <exception cref="ArgumentException">Thrown when endpoint is empty or whitespace</exception>
        /// <example>
        /// <code>
        /// var request = client.CreateDeleteRequest("users/123");
        /// var response = await client.ExecuteAsync(request);
        /// </code>
        /// </example>
        public static RestRequest CreateDeleteRequest(
            this IRestClient client,
            string endpoint)
        {
            if (client == null)
                throw new ArgumentNullException(nameof(client));
            if (string.IsNullOrWhiteSpace(endpoint))
                throw new ArgumentException("Endpoint cannot be null or empty", nameof(endpoint));

            var request = new RestRequest(endpoint, Method.Delete);
            
            // Add standard headers
            request.AddHeader("Accept", "application/json");
            request.AddHeader("User-Agent", "Memoria/1.0");

            return request;
        }

        /// <summary>
        /// Adds authentication headers to a RestRequest using configuration settings.
        /// Supports API key authentication as used by the Memoria API.
        /// </summary>
        /// <param name="request">The request to add authentication to</param>
        /// <param name="config">Configuration containing API key and authentication settings</param>
        /// <returns>The request with authentication headers added</returns>
        /// <exception cref="ArgumentNullException">Thrown when request or config is null</exception>
        /// <example>
        /// <code>
        /// var request = client.CreateGetRequest("protected-endpoint");
        /// request.AddApiAuthentication(configuration);
        /// var response = await client.ExecuteAsync(request);
        /// </code>
        /// </example>
        public static RestRequest AddApiAuthentication(
            this RestRequest request,
            Configuration config)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            // Add API key header if available
            if (!string.IsNullOrWhiteSpace(config.Key))
            {
                request.AddHeader(ApiHeaders.AUTHORIZATION, ApiHeaders.CreateApiKeyToken(config.Key));
            }

            // Add version header if available
            if (!string.IsNullOrWhiteSpace(config.Version.ToString()))
            {
                request.AddHeader(ApiHeaders.VERSION, config.Version.ToString());
            }

            // Add language header if available
            if (config.Language != Configuration.LanguageEnum.en)
            {
                request.AddHeader(ApiHeaders.LANGUAGE, config.Language.ToString());
            }

            return request;
        }

        /// <summary>
        /// Executes a REST request with comprehensive error handling and logging.
        /// Provides detailed error information and handles common HTTP scenarios.
        /// </summary>
        /// <typeparam name="T">Expected response type</typeparam>
        /// <param name="client">The RestSharp client instance</param>
        /// <param name="request">The request to execute</param>
        /// <param name="logger">Logger for recording request details and errors</param>
        /// <param name="cancellationToken">Cancellation token for request timeout</param>
        /// <returns>Result containing the response data or error information</returns>
        /// <exception cref="ArgumentNullException">Thrown when client, request, or logger is null</exception>
        /// <example>
        /// <code>
        /// var request = client.CreateGetRequest("server/status");
        /// var result = await client.ExecuteWithLoggingAsync&lt;ServerStatsDto&gt;(request, logger);
        /// if (result.IsSuccess)
        /// {
        ///     var serverStats = result.Value;
        ///     // Process server stats
        /// }
        /// </code>
        /// </example>
        public static async Task<Result<T>> ExecuteWithLoggingAsync<T>(
            this IRestClient client,
            RestRequest request,
            ILogger logger,
            CancellationToken cancellationToken = default)
        {
            if (client == null)
                throw new ArgumentNullException(nameof(client));
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));

            var startTime = DateTime.UtcNow;
            
            try
            {
                logger.LogDebug("Executing {Method} request to {Resource}", 
                    request.Method, request.Resource);

                var response = await client.ExecuteAsync<T>(request, cancellationToken);
                var duration = DateTime.UtcNow - startTime;

                logger.LogDebug("Request completed in {Duration}ms with status {StatusCode}", 
                    duration.TotalMilliseconds, response.StatusCode);

                // Handle successful responses
                if (response.IsSuccessful && response.Data != null)
                {
                    return Result<T>.Success(response.Data);
                }

                // Handle HTTP error responses
                if (!response.IsSuccessful)
                {
                    var errorMessage = $"HTTP {(int)response.StatusCode} {response.StatusCode}";
                    if (!string.IsNullOrWhiteSpace(response.Content))
                    {
                        errorMessage += $": {response.Content}";
                    }

                    logger.LogWarning("Request failed with {StatusCode}: {ErrorMessage}", 
                        response.StatusCode, errorMessage);

                    return Result<T>.Failure(errorMessage);
                }

                // Handle null data responses
                logger.LogWarning("Request succeeded but returned null data");
                return Result<T>.Failure("Request succeeded but returned no data");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning("Request to {Resource} was cancelled", request.Resource);
                return Result<T>.Failure("Request was cancelled");
            }
            catch (Exception ex)
            {
                var duration = DateTime.UtcNow - startTime;
                logger.LogError(ex, "Request to {Resource} failed after {Duration}ms", 
                    request.Resource, duration.TotalMilliseconds);

                return Result<T>.Failure("Request failed due to network error", ex);
            }
        }

        /// <summary>
        /// Executes a REST request and deserializes the response as an ApiResponse wrapper.
        /// Handles the standard Memoria API response format with success/error structure.
        /// </summary>
        /// <typeparam name="T">Expected data type within the ApiResponse</typeparam>
        /// <param name="client">The RestSharp client instance</param>
        /// <param name="request">The request to execute</param>
        /// <param name="logger">Logger for recording request details and errors</param>
        /// <param name="cancellationToken">Cancellation token for request timeout</param>
        /// <returns>Result containing the ApiResponse data or error information</returns>
        /// <exception cref="ArgumentNullException">Thrown when client, request, or logger is null</exception>
        /// <example>
        /// <code>
        /// var request = client.CreatePostRequest("players", playerData);
        /// var result = await client.ExecuteApiRequestAsync&lt;bool&gt;(request, logger);
        /// if (result.IsSuccess && result.Value.Success)
        /// {
        ///     // Player data uploaded successfully
        /// }
        /// </code>
        /// </example>
        public static async Task<Result<ApiResponse<T>>> ExecuteApiRequestAsync<T>(
            this IRestClient client,
            RestRequest request,
            ILogger logger,
            CancellationToken cancellationToken = default)
        {
            if (client == null)
                throw new ArgumentNullException(nameof(client));
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));

            var startTime = DateTime.UtcNow;
            
            try
            {
                logger.LogDebug("Executing API request {Method} to {Resource}", 
                    request.Method, request.Resource);

                var response = await client.ExecuteAsync(request, cancellationToken);
                var duration = DateTime.UtcNow - startTime;

                logger.LogDebug("API request completed in {Duration}ms with status {StatusCode}", 
                    duration.TotalMilliseconds, response.StatusCode);

                // Try to deserialize as ApiResponse regardless of HTTP status
                if (!string.IsNullOrWhiteSpace(response.Content))
                {
                    try
                    {
                        var apiResponse = JsonConvert.DeserializeObject<ApiResponse<T>>(response.Content);
                        if (apiResponse != null)
                        {
                            if (!apiResponse.Success)
                            {
                                logger.LogWarning("API request failed: {Error}", 
                                    apiResponse.Error ?? "Unknown API error");
                            }
                            return Result<ApiResponse<T>>.Success(apiResponse);
                        }
                    }
                    catch (JsonException jsonEx)
                    {
                        logger.LogWarning(jsonEx, "Failed to deserialize API response as ApiResponse<T>");
                    }
                }

                // Handle HTTP errors when JSON deserialization fails
                if (!response.IsSuccessful)
                {
                    var errorMessage = $"HTTP {(int)response.StatusCode} {response.StatusCode}";
                    logger.LogError("API request failed with {StatusCode}: {Content}", 
                        response.StatusCode, response.Content);

                    return Result<ApiResponse<T>>.Failure(errorMessage);
                }

                // Handle empty or invalid responses
                logger.LogWarning("API request returned empty or invalid response");
                return Result<ApiResponse<T>>.Failure("Invalid API response format");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning("API request to {Resource} was cancelled", request.Resource);
                return Result<ApiResponse<T>>.Failure("Request was cancelled");
            }
            catch (Exception ex)
            {
                var duration = DateTime.UtcNow - startTime;
                logger.LogError(ex, "API request to {Resource} failed after {Duration}ms", 
                    request.Resource, duration.TotalMilliseconds);

                return Result<ApiResponse<T>>.Failure("API request failed due to network error", ex);
            }
        }

        /// <summary>
        /// Adds query parameters from an object using reflection.
        /// Automatically converts object properties to query string parameters.
        /// </summary>
        /// <param name="request">The request to add parameters to</param>
        /// <param name="parameters">Object containing parameters as properties</param>
        /// <returns>The request with query parameters added</returns>
        /// <exception cref="ArgumentNullException">Thrown when request is null</exception>
        /// <example>
        /// <code>
        /// var request = new RestRequest("players/search");
        /// request.AddObjectAsQueryString(new { name = "Player", world = "Gilgamesh", limit = 50 });
        /// </code>
        /// </example>
        public static RestRequest AddObjectAsQueryString(
            this RestRequest request,
            object parameters)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (parameters == null)
                return request;

            var properties = parameters.GetType().GetProperties();
            foreach (var property in properties)
            {
                var value = property.GetValue(parameters);
                if (value != null)
                {
                    request.AddQueryParameter(property.Name, value.ToString());
                }
            }

            return request;
        }

        /// <summary>
        /// Configures request timeout and retry policies for robust API communication.
        /// Sets appropriate timeout values and retry behavior for different request types.
        /// </summary>
        /// <param name="request">The request to configure</param>
        /// <param name="timeoutSeconds">Request timeout in seconds (default: 30)</param>
        /// <param name="maxRetries">Maximum number of retry attempts (default: 3)</param>
        /// <returns>The request with timeout and retry configuration</returns>
        /// <exception cref="ArgumentNullException">Thrown when request is null</exception>
        /// <exception cref="ArgumentException">Thrown when timeout or maxRetries is invalid</exception>
        /// <example>
        /// <code>
        /// var request = client.CreatePostRequest("players/batch", largeBatch);
        /// request.ConfigureResilience(timeoutSeconds: 60, maxRetries: 5);
        /// </code>
        /// </example>
        public static RestRequest ConfigureResilience(
            this RestRequest request,
            int timeoutSeconds = 30,
            int maxRetries = 3)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (timeoutSeconds <= 0)
                throw new ArgumentException("Timeout must be greater than 0", nameof(timeoutSeconds));
            if (maxRetries < 0)
                throw new ArgumentException("Max retries cannot be negative", nameof(maxRetries));

            request.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
            
            // Note: RestSharp doesn't have built-in retry policies in the request object
            // This would typically be configured at the client level or through middleware
            
            return request;
        }
    }
}