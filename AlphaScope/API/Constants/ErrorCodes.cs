using System.Net;

namespace AlphaScope.API.Constants
{
    /// <summary>
    /// Contains standardized error codes and HTTP status codes used throughout the AlphaScope API services.
    /// This centralizes error handling and ensures consistent error reporting across all API operations.
    /// </summary>
    public static class ErrorCodes
    {
        #region HTTP Status Codes
        
        /// <summary>
        /// Request was successful (200)
        /// </summary>
        public const int OK = (int)HttpStatusCode.OK;
        
        /// <summary>
        /// Bad Request - The request was invalid or malformed (400)
        /// </summary>
        public const int BAD_REQUEST = (int)HttpStatusCode.BadRequest;
        
        /// <summary>
        /// Unauthorized - Authentication failed or not provided (401)
        /// </summary>
        public const int UNAUTHORIZED = (int)HttpStatusCode.Unauthorized;
        
        /// <summary>
        /// Forbidden - Request was valid but server refused to authorize it (403)
        /// </summary>
        public const int FORBIDDEN = (int)HttpStatusCode.Forbidden;
        
        /// <summary>
        /// Not Found - The requested resource was not found (404)
        /// </summary>
        public const int NOT_FOUND = (int)HttpStatusCode.NotFound;
        
        /// <summary>
        /// Request Timeout - The request took too long to complete (408)
        /// </summary>
        public const int REQUEST_TIMEOUT = (int)HttpStatusCode.RequestTimeout;
        
        /// <summary>
        /// Unprocessable Entity - Request was well-formed but contained semantic errors (422)
        /// </summary>
        public const int UNPROCESSABLE_ENTITY = 422;
        
        /// <summary>
        /// Internal Server Error - An unexpected error occurred on the server (500)
        /// </summary>
        public const int INTERNAL_SERVER_ERROR = (int)HttpStatusCode.InternalServerError;
        
        /// <summary>
        /// Service Unavailable - The server is temporarily unable to handle the request (503)
        /// </summary>
        public const int SERVICE_UNAVAILABLE = (int)HttpStatusCode.ServiceUnavailable;
        
        #endregion
        
        #region Custom Error Codes
        
        /// <summary>
        /// Indicates that the operation was cancelled by the user or system
        /// </summary>
        public const int OPERATION_CANCELLED = 408;
        
        /// <summary>
        /// Indicates a network connectivity issue
        /// </summary>
        public const int NETWORK_ERROR = 503;
        
        /// <summary>
        /// Indicates a configuration validation failure
        /// </summary>
        public const int CONFIGURATION_ERROR = 400;
        
        /// <summary>
        /// Indicates a JSON parsing or serialization error
        /// </summary>
        public const int JSON_ERROR = 422;
        
        #endregion
        
        #region Error Messages
        
        /// <summary>
        /// Standard message for cancelled operations
        /// </summary>
        public const string OPERATION_CANCELLED_MESSAGE = "Operation was cancelled";
        
        /// <summary>
        /// Standard message for timeout errors
        /// </summary>
        public const string REQUEST_TIMEOUT_MESSAGE = "Request timed out";
        
        /// <summary>
        /// Standard message for network connection failures
        /// </summary>
        public const string NETWORK_CONNECTION_FAILED_MESSAGE = "Network connection failed";
        
        /// <summary>
        /// Standard message for authentication failures
        /// </summary>
        public const string AUTHENTICATION_FAILED_MESSAGE = "Authentication failed";
        
        /// <summary>
        /// Standard message for configuration validation failures
        /// </summary>
        public const string CONFIGURATION_VALIDATION_FAILED_MESSAGE = "Configuration validation failed";
        
        /// <summary>
        /// Standard message for invalid server responses
        /// </summary>
        public const string INVALID_SERVER_RESPONSE_MESSAGE = "Invalid server response";
        
        /// <summary>
        /// Standard message for health check failures
        /// </summary>
        public const string HEALTH_CHECK_FAILED_MESSAGE = "Health check failed";
        
        /// <summary>
        /// Standard message for version info retrieval failures
        /// </summary>
        public const string VERSION_INFO_RETRIEVAL_FAILED_MESSAGE = "Version info retrieval failed";
        
        /// <summary>
        /// Standard message for configuration update failures
        /// </summary>
        public const string CONFIGURATION_UPDATE_FAILED_MESSAGE = "Configuration update failed";
        
        /// <summary>
        /// Standard message for diagnostics retrieval failures
        /// </summary>
        public const string DIAGNOSTICS_RETRIEVAL_FAILED_MESSAGE = "Diagnostics retrieval failed";
        
        #endregion
        
        #region Validation Error Messages
        
        /// <summary>
        /// Error message for null or empty parameters
        /// </summary>
        public const string PARAMETER_CANNOT_BE_NULL_MESSAGE = "Parameter cannot be null";
        
        /// <summary>
        /// Error message for invalid URL parameters
        /// </summary>
        public const string INVALID_URL_MESSAGE = "URL is not valid";
        
        /// <summary>
        /// Error message for invalid timeout values
        /// </summary>
        public const string TIMEOUT_MUST_BE_GREATER_THAN_ZERO_MESSAGE = "Timeout must be greater than 0";
        
        /// <summary>
        /// Error message for invalid player ID values
        /// </summary>
        public const string PLAYER_ID_MUST_BE_GREATER_THAN_ZERO_MESSAGE = "Player ID must be greater than 0";
        
        /// <summary>
        /// Error message for empty collections
        /// </summary>
        public const string COLLECTION_CANNOT_BE_EMPTY_MESSAGE = "Collection cannot be empty";
        
        /// <summary>
        /// Error message for configuration requirements
        /// </summary>
        public const string BASE_URL_IS_REQUIRED_MESSAGE = "Base URL is required";
        
        /// <summary>
        /// Error message for configuration setup
        /// </summary>
        public const string BASE_URL_NOT_CONFIGURED_MESSAGE = "Base URL is not configured";
        
        #endregion
        
        #region Helper Methods
        
        /// <summary>
        /// Determines if an HTTP status code represents a successful response
        /// </summary>
        /// <param name="statusCode">The HTTP status code to check</param>
        /// <returns>True if the status code represents success (2xx range)</returns>
        public static bool IsSuccessStatusCode(int statusCode)
        {
            return statusCode >= 200 && statusCode <= 299;
        }
        
        /// <summary>
        /// Determines if an HTTP status code represents a client error
        /// </summary>
        /// <param name="statusCode">The HTTP status code to check</param>
        /// <returns>True if the status code represents a client error (4xx range)</returns>
        public static bool IsClientError(int statusCode)
        {
            return statusCode >= 400 && statusCode <= 499;
        }
        
        /// <summary>
        /// Determines if an HTTP status code represents a server error
        /// </summary>
        /// <param name="statusCode">The HTTP status code to check</param>
        /// <returns>True if the status code represents a server error (5xx range)</returns>
        public static bool IsServerError(int statusCode)
        {
            return statusCode >= 500 && statusCode <= 599;
        }
        
        /// <summary>
        /// Gets a standard error message for common HTTP status codes
        /// </summary>
        /// <param name="statusCode">The HTTP status code</param>
        /// <returns>A user-friendly error message for the status code</returns>
        public static string GetStandardErrorMessage(int statusCode)
        {
            return statusCode switch
            {
                BAD_REQUEST => "The request was invalid or malformed",
                UNAUTHORIZED => "Authentication is required to access this resource",
                FORBIDDEN => "You do not have permission to access this resource",
                NOT_FOUND => "The requested resource was not found",
                REQUEST_TIMEOUT => REQUEST_TIMEOUT_MESSAGE,
                UNPROCESSABLE_ENTITY => "The request contained invalid data",
                INTERNAL_SERVER_ERROR => "An internal server error occurred",
                SERVICE_UNAVAILABLE => "The service is temporarily unavailable",
                _ => "An unexpected error occurred"
            };
        }
        
        #endregion
    }
}