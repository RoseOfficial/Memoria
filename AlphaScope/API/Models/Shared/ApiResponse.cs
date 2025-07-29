using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AlphaScope.API.Models.Shared
{
    /// <summary>
    /// Standardized API response wrapper for all API endpoints
    /// </summary>
    /// <typeparam name="T">The type of data being returned</typeparam>
    public class ApiResponse<T>
    {
        /// <summary>
        /// Indicates whether the API call was successful
        /// </summary>
        [JsonProperty("success")]
        public bool Success { get; set; }

        /// <summary>
        /// The main data payload of the response
        /// </summary>
        [JsonProperty("data")]
        public T? Data { get; set; }

        /// <summary>
        /// Error message if the request failed
        /// </summary>
        [JsonProperty("error")]
        public string? Error { get; set; }

        /// <summary>
        /// Additional error details for debugging
        /// </summary>
        [JsonProperty("errorDetails")]
        public string? ErrorDetails { get; set; }

        /// <summary>
        /// HTTP status code
        /// </summary>
        [JsonProperty("statusCode")]
        public int StatusCode { get; set; }

        /// <summary>
        /// Server timestamp when the response was generated
        /// </summary>
        [JsonProperty("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Creates a successful API response
        /// </summary>
        /// <param name="data">The data to return</param>
        /// <param name="statusCode">HTTP status code (default: 200)</param>
        /// <returns>A successful ApiResponse</returns>
        public static ApiResponse<T> Ok(T data, int statusCode = 200)
        {
            return new ApiResponse<T>
            {
                Success = true,
                Data = data,
                StatusCode = statusCode,
                Timestamp = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Creates a failed API response
        /// </summary>
        /// <param name="error">Error message</param>
        /// <param name="statusCode">HTTP status code (default: 400)</param>
        /// <param name="errorDetails">Additional error details</param>
        /// <returns>A failed ApiResponse</returns>
        public static ApiResponse<T> Fail(string error, int statusCode = 400, string? errorDetails = null)
        {
            return new ApiResponse<T>
            {
                Success = false,
                Error = error,
                ErrorDetails = errorDetails,
                StatusCode = statusCode,
                Timestamp = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// Non-generic version of ApiResponse for endpoints that don't return data
    /// </summary>
    public class ApiResponse : ApiResponse<object>
    {
        /// <summary>
        /// Creates a successful API response without data
        /// </summary>
        /// <param name="statusCode">HTTP status code (default: 200)</param>
        /// <returns>A successful ApiResponse</returns>
        public static ApiResponse Ok(int statusCode = 200)
        {
            return new ApiResponse
            {
                Success = true,
                StatusCode = statusCode,
                Timestamp = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Creates a failed API response without data
        /// </summary>
        /// <param name="error">Error message</param>
        /// <param name="statusCode">HTTP status code (default: 400)</param>
        /// <param name="errorDetails">Additional error details</param>
        /// <returns>A failed ApiResponse</returns>
        public static new ApiResponse Fail(string error, int statusCode = 400, string? errorDetails = null)
        {
            return new ApiResponse
            {
                Success = false,
                Error = error,
                ErrorDetails = errorDetails,
                StatusCode = statusCode,
                Timestamp = DateTime.UtcNow
            };
        }
    }
}