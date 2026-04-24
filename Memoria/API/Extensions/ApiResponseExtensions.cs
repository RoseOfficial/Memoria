using System;
using System.Threading.Tasks;
using Memoria.API.Client;
using Memoria.API.Models.Shared;

namespace Memoria.API.Extensions
{
    /// <summary>
    /// Extension methods for Result&lt;T&gt; and ApiResponse&lt;T&gt; types to provide
    /// convenient conversion, validation, and error handling operations.
    /// Simplifies working with the Memoria API response patterns and provides
    /// fluent interfaces for common operations.
    /// </summary>
    public static class ApiResponseExtensions
    {
        // ========== Result<T> Extensions ==========

        /// <summary>
        /// Converts a Result&lt;T&gt; to an ApiResponse&lt;T&gt; format.
        /// Useful for standardizing response formats across different layers of the application.
        /// </summary>
        /// <typeparam name="T">The type of data contained in the result</typeparam>
        /// <param name="result">The result to convert</param>
        /// <param name="statusCode">HTTP status code to assign (default: 200 for success, 400 for failure)</param>
        /// <returns>An ApiResponse equivalent of the Result</returns>
        /// <exception cref="ArgumentNullException">Thrown when result is null</exception>
        /// <example>
        /// <code>
        /// var result = Result&lt;PlayerDto&gt;.Success(playerData);
        /// var apiResponse = result.ToApiResponse();
        /// // apiResponse.Success == true, apiResponse.Data == playerData
        /// </code>
        /// </example>
        public static ApiResponse<T> ToApiResponse<T>(this Result<T> result, int? statusCode = null)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));

            if (result.IsSuccess)
            {
                return ApiResponse<T>.Ok(result.Value!, statusCode ?? 200);
            }
            else
            {
                return ApiResponse<T>.Fail(
                    result.Error ?? "Operation failed", 
                    statusCode ?? 400, 
                    result.Exception?.ToString());
            }
        }

        /// <summary>
        /// Executes an action if the Result is successful, otherwise returns the failure unchanged.
        /// Provides functional programming style chaining for Result operations.
        /// </summary>
        /// <typeparam name="T">The type of data in the result</typeparam>
        /// <param name="result">The result to operate on</param>
        /// <param name="onSuccess">Action to execute if the result is successful</param>
        /// <returns>The original result for method chaining</returns>
        /// <exception cref="ArgumentNullException">Thrown when result or onSuccess is null</exception>
        /// <example>
        /// <code>
        /// var result = await apiClient.GetPlayerByIdAsync(playerId);
        /// result.OnSuccess(player => logger.LogInformation("Found player: {Name}", player.Name));
        /// </code>
        /// </example>
        public static Result<T> OnSuccess<T>(this Result<T> result, Action<T> onSuccess)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));
            if (onSuccess == null)
                throw new ArgumentNullException(nameof(onSuccess));

            if (result.IsSuccess)
            {
                onSuccess(result.Value!);
            }

            return result;
        }

        /// <summary>
        /// Executes an action if the Result is a failure, otherwise returns the success unchanged.
        /// Useful for logging errors or performing cleanup operations on failure.
        /// </summary>
        /// <typeparam name="T">The type of data in the result</typeparam>
        /// <param name="result">The result to operate on</param>
        /// <param name="onFailure">Action to execute if the result is a failure</param>
        /// <returns>The original result for method chaining</returns>
        /// <exception cref="ArgumentNullException">Thrown when result or onFailure is null</exception>
        /// <example>
        /// <code>
        /// var result = await apiClient.PostPlayersAsync(playerData);
        /// result.OnFailure(error => logger.LogError("Upload failed: {Error}", error));
        /// </code>
        /// </example>
        public static Result<T> OnFailure<T>(this Result<T> result, Action<string> onFailure)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));
            if (onFailure == null)
                throw new ArgumentNullException(nameof(onFailure));

            if (result.IsFailure)
            {
                onFailure(result.Error ?? "Unknown error");
            }

            return result;
        }

        /// <summary>
        /// Transforms the value of a successful Result using the provided function.
        /// If the Result is a failure, returns a failure with the same error.
        /// </summary>
        /// <typeparam name="TSource">The source type of the result</typeparam>
        /// <typeparam name="TResult">The target type after transformation</typeparam>
        /// <param name="result">The result to transform</param>
        /// <param name="transform">Function to transform the success value</param>
        /// <returns>A new Result with the transformed value or the original failure</returns>
        /// <exception cref="ArgumentNullException">Thrown when result or transform is null</exception>
        /// <example>
        /// <code>
        /// var playerResult = await apiClient.GetPlayerByIdAsync(playerId);
        /// var nameResult = playerResult.Map(player => player.Name);
        /// // nameResult contains just the player name if successful
        /// </code>
        /// </example>
        public static Result<TResult> Map<TSource, TResult>(
            this Result<TSource> result, 
            Func<TSource, TResult> transform)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));
            if (transform == null)
                throw new ArgumentNullException(nameof(transform));

            if (result.IsSuccess)
            {
                try
                {
                    var transformedValue = transform(result.Value!);
                    return Result<TResult>.Success(transformedValue);
                }
                catch (Exception ex)
                {
                    return Result<TResult>.Failure("Transformation failed", ex);
                }
            }

            return Result<TResult>.Failure(result.Error ?? "Unknown error", result.Exception);
        }

        /// <summary>
        /// Chains two Result operations together, where the second operation depends on the first.
        /// If the first Result fails, the second operation is not executed.
        /// </summary>
        /// <typeparam name="TSource">The source type of the first result</typeparam>
        /// <typeparam name="TResult">The target type of the second result</typeparam>
        /// <param name="result">The first result</param>
        /// <param name="next">Function that takes the first result's value and returns a new Result</param>
        /// <returns>The result of the second operation, or the first failure</returns>
        /// <exception cref="ArgumentNullException">Thrown when result or next is null</exception>
        /// <example>
        /// <code>
        /// var result = await apiClient.GetPlayerByIdAsync(playerId)
        ///     .Bind(async player => await apiClient.UpdatePlayerAsync(player.Id, updateData));
        /// // Only updates if the player retrieval was successful
        /// </code>
        /// </example>
        public static Result<TResult> Bind<TSource, TResult>(
            this Result<TSource> result,
            Func<TSource, Result<TResult>> next)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));
            if (next == null)
                throw new ArgumentNullException(nameof(next));

            if (result.IsSuccess)
            {
                try
                {
                    return next(result.Value!);
                }
                catch (Exception ex)
                {
                    return Result<TResult>.Failure("Chained operation failed", ex);
                }
            }

            return Result<TResult>.Failure(result.Error ?? "Unknown error", result.Exception);
        }

        /// <summary>
        /// Asynchronously chains two Result operations together.
        /// Similar to Bind but supports async operations in the chain.
        /// </summary>
        /// <typeparam name="TSource">The source type of the first result</typeparam>
        /// <typeparam name="TResult">The target type of the second result</typeparam>
        /// <param name="result">The first result</param>
        /// <param name="next">Async function that takes the first result's value and returns a new Result</param>
        /// <returns>Task containing the result of the second operation, or the first failure</returns>
        /// <exception cref="ArgumentNullException">Thrown when result or next is null</exception>
        /// <example>
        /// <code>
        /// var result = await apiClient.GetPlayerByIdAsync(playerId)
        ///     .BindAsync(async player => await apiClient.UpdatePlayerStatsAsync(player.Id));
        /// </code>
        /// </example>
        public static async Task<Result<TResult>> BindAsync<TSource, TResult>(
            this Result<TSource> result,
            Func<TSource, Task<Result<TResult>>> next)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));
            if (next == null)
                throw new ArgumentNullException(nameof(next));

            if (result.IsSuccess)
            {
                try
                {
                    return await next(result.Value!);
                }
                catch (Exception ex)
                {
                    return Result<TResult>.Failure("Async chained operation failed", ex);
                }
            }

            return Result<TResult>.Failure(result.Error ?? "Unknown error", result.Exception);
        }

        // ========== ApiResponse<T> Extensions ==========

        /// <summary>
        /// Converts an ApiResponse&lt;T&gt; to a Result&lt;T&gt; format.
        /// Useful for integrating with code that expects Result types.
        /// </summary>
        /// <typeparam name="T">The type of data contained in the response</typeparam>
        /// <param name="apiResponse">The API response to convert</param>
        /// <returns>A Result equivalent of the ApiResponse</returns>
        /// <exception cref="ArgumentNullException">Thrown when apiResponse is null</exception>
        /// <example>
        /// <code>
        /// var apiResponse = ApiResponse&lt;PlayerDto&gt;.Ok(playerData);
        /// var result = apiResponse.ToResult();
        /// // result.IsSuccess == true, result.Value == playerData
        /// </code>
        /// </example>
        public static Result<T> ToResult<T>(this ApiResponse<T> apiResponse)
        {
            if (apiResponse == null)
                throw new ArgumentNullException(nameof(apiResponse));

            if (apiResponse.Success && apiResponse.Data != null)
            {
                return Result<T>.Success(apiResponse.Data);
            }
            else
            {
                var errorMessage = apiResponse.Error ?? "API request failed";
                if (!string.IsNullOrWhiteSpace(apiResponse.ErrorDetails))
                {
                    errorMessage += $" - Details: {apiResponse.ErrorDetails}";
                }
                return Result<T>.Failure(errorMessage);
            }
        }

        /// <summary>
        /// Validates that an ApiResponse contains successful data.
        /// Checks both the Success flag and ensures Data is not null.
        /// </summary>
        /// <typeparam name="T">The type of data expected in the response</typeparam>
        /// <param name="apiResponse">The API response to validate</param>
        /// <returns>True if the response is successful and contains data, false otherwise</returns>
        /// <exception cref="ArgumentNullException">Thrown when apiResponse is null</exception>
        /// <example>
        /// <code>
        /// var response = await client.ExecuteApiRequestAsync&lt;PlayerDto&gt;(request, logger);
        /// if (response.IsSuccess && response.Value.IsSuccessfulWithData())
        /// {
        ///     var playerData = response.Value.Data;
        ///     // Process valid player data
        /// }
        /// </code>
        /// </example>
        public static bool IsSuccessfulWithData<T>(this ApiResponse<T> apiResponse)
        {
            if (apiResponse == null)
                throw new ArgumentNullException(nameof(apiResponse));

            return apiResponse.Success && apiResponse.Data != null;
        }

        /// <summary>
        /// Gets the error message from an ApiResponse, combining Error and ErrorDetails.
        /// Provides a comprehensive error description for logging or user display.
        /// </summary>
        /// <typeparam name="T">The type of data in the response</typeparam>
        /// <param name="apiResponse">The API response to extract error from</param>
        /// <returns>Combined error message, or empty string if successful</returns>
        /// <exception cref="ArgumentNullException">Thrown when apiResponse is null</exception>
        /// <example>
        /// <code>
        /// var response = await client.ExecuteApiRequestAsync&lt;PlayerDto&gt;(request, logger);
        /// if (response.IsSuccess && !response.Value.Success)
        /// {
        ///     var errorMessage = response.Value.GetFullErrorMessage();
        ///     logger.LogError("API error: {Error}", errorMessage);
        /// }
        /// </code>
        /// </example>
        public static string GetFullErrorMessage<T>(this ApiResponse<T> apiResponse)
        {
            if (apiResponse == null)
                throw new ArgumentNullException(nameof(apiResponse));

            if (apiResponse.Success)
                return string.Empty;

            var message = apiResponse.Error ?? "Unknown error";
            if (!string.IsNullOrWhiteSpace(apiResponse.ErrorDetails))
            {
                message += $" - Details: {apiResponse.ErrorDetails}";
            }

            return message;
        }

        /// <summary>
        /// Extracts data from an ApiResponse with a fallback value if unsuccessful.
        /// Provides safe access to response data with default handling.
        /// </summary>
        /// <typeparam name="T">The type of data in the response</typeparam>
        /// <param name="apiResponse">The API response to extract data from</param>
        /// <param name="fallbackValue">Value to return if the response is unsuccessful or data is null</param>
        /// <returns>The response data if successful, otherwise the fallback value</returns>
        /// <exception cref="ArgumentNullException">Thrown when apiResponse is null</exception>
        /// <example>
        /// <code>
        /// var response = await client.ExecuteApiRequestAsync&lt;List&lt;PlayerDto&gt;&gt;(request, logger);
        /// var players = response.IsSuccess 
        ///     ? response.Value.GetDataOrDefault(new List&lt;PlayerDto&gt;())
        ///     : new List&lt;PlayerDto&gt;();
        /// </code>
        /// </example>
        public static T? GetDataOrDefault<T>(this ApiResponse<T> apiResponse, T? fallbackValue = default(T))
        {
            if (apiResponse == null)
                throw new ArgumentNullException(nameof(apiResponse));

            return apiResponse.IsSuccessfulWithData() ? apiResponse.Data : fallbackValue;
        }

        /// <summary>
        /// Transforms the data in an ApiResponse using the provided function.
        /// If the ApiResponse is unsuccessful, returns a new unsuccessful response.
        /// </summary>
        /// <typeparam name="TSource">The source type of the API response data</typeparam>
        /// <typeparam name="TResult">The target type after transformation</typeparam>
        /// <param name="apiResponse">The API response to transform</param>
        /// <param name="transform">Function to transform the success data</param>
        /// <returns>A new ApiResponse with the transformed data or the original error</returns>
        /// <exception cref="ArgumentNullException">Thrown when apiResponse or transform is null</exception>
        /// <example>
        /// <code>
        /// var playerResponse = await client.ExecuteApiRequestAsync&lt;PlayerDto&gt;(request, logger);
        /// var nameResponse = playerResponse.IsSuccess 
        ///     ? playerResponse.Value.MapData(player => player.Name)
        ///     : ApiResponse&lt;string&gt;.Fail("Failed to get player");
        /// </code>
        /// </example>
        public static ApiResponse<TResult> MapData<TSource, TResult>(
            this ApiResponse<TSource> apiResponse,
            Func<TSource, TResult> transform)
        {
            if (apiResponse == null)
                throw new ArgumentNullException(nameof(apiResponse));
            if (transform == null)
                throw new ArgumentNullException(nameof(transform));

            if (apiResponse.IsSuccessfulWithData())
            {
                try
                {
                    var transformedData = transform(apiResponse.Data!);
                    return ApiResponse<TResult>.Ok(transformedData, apiResponse.StatusCode);
                }
                catch (Exception ex)
                {
                    return ApiResponse<TResult>.Fail(
                        "Data transformation failed", 
                        500, 
                        ex.ToString());
                }
            }

            return ApiResponse<TResult>.Fail(
                apiResponse.Error ?? "Request failed",
                apiResponse.StatusCode,
                apiResponse.ErrorDetails);
        }

        /// <summary>
        /// Validates an ApiResponse against custom business rules.
        /// Allows for additional validation beyond the standard Success flag.
        /// </summary>
        /// <typeparam name="T">The type of data in the response</typeparam>
        /// <param name="apiResponse">The API response to validate</param>
        /// <param name="validator">Function that validates the response data</param>
        /// <param name="validationErrorMessage">Error message to use if validation fails</param>
        /// <returns>The original response if valid, or a new failure response if invalid</returns>
        /// <exception cref="ArgumentNullException">Thrown when apiResponse or validator is null</exception>
        /// <example>
        /// <code>
        /// var response = await client.ExecuteApiRequestAsync&lt;PlayerDto&gt;(request, logger);
        /// var validatedResponse = response.IsSuccess 
        ///     ? response.Value.ValidateData(
        ///         player => player.Level > 0, 
        ///         "Player level must be greater than 0")
        ///     : response.Value;
        /// </code>
        /// </example>
        public static ApiResponse<T> ValidateData<T>(
            this ApiResponse<T> apiResponse,
            Func<T, bool> validator,
            string validationErrorMessage = "Data validation failed")
        {
            if (apiResponse == null)
                throw new ArgumentNullException(nameof(apiResponse));
            if (validator == null)
                throw new ArgumentNullException(nameof(validator));

            if (!apiResponse.IsSuccessfulWithData())
                return apiResponse;

            try
            {
                if (validator(apiResponse.Data!))
                {
                    return apiResponse;
                }
                else
                {
                    return ApiResponse<T>.Fail(
                        validationErrorMessage ?? "Data validation failed",
                        400);
                }
            }
            catch (Exception ex)
            {
                return ApiResponse<T>.Fail(
                    "Validation error occurred",
                    500,
                    ex.ToString());
            }
        }
    }
}