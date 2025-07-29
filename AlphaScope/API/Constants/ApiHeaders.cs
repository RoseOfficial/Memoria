namespace AlphaScope.API.Constants
{
    /// <summary>
    /// Contains all HTTP header constants used throughout the AlphaScope API services.
    /// This centralizes header management and ensures consistent naming across all API calls.
    /// </summary>
    public static class ApiHeaders
    {
        #region Custom API Headers
        
        /// <summary>
        /// Version header - indicates the client version making the API request
        /// Used to track client compatibility and version-specific behavior
        /// </summary>
        public const string VERSION = "V";
        
        /// <summary>
        /// Language header - indicates the user's preferred language
        /// Used for localization and language-specific responses
        /// </summary>
        public const string LANGUAGE = "L";
        
        #endregion
        
        #region Standard HTTP Headers
        
        /// <summary>
        /// Content-Type header for JSON requests
        /// </summary>
        public const string CONTENT_TYPE = "Content-Type";
        
        /// <summary>
        /// Accept header for specifying response format
        /// </summary>
        public const string ACCEPT = "Accept";
        
        /// <summary>
        /// Authorization header for API key or token authentication
        /// </summary>
        public const string AUTHORIZATION = "Authorization";
        
        /// <summary>
        /// User-Agent header for client identification
        /// </summary>
        public const string USER_AGENT = "User-Agent";
        
        #endregion
        
        #region Header Values
        
        /// <summary>
        /// JSON content type value
        /// </summary>
        public const string APPLICATION_JSON = "application/json";
        
        /// <summary>
        /// Form URL encoded content type value
        /// </summary>
        public const string APPLICATION_FORM_URLENCODED = "application/x-www-form-urlencoded";
        
        /// <summary>
        /// Text/plain content type value
        /// </summary>
        public const string TEXT_PLAIN = "text/plain";
        
        /// <summary>
        /// Bearer token authorization prefix
        /// </summary>
        public const string BEARER_PREFIX = "Bearer ";
        
        /// <summary>
        /// API key authorization prefix
        /// </summary>
        public const string API_KEY_PREFIX = "ApiKey ";
        
        #endregion
        
        #region Helper Methods
        
        /// <summary>
        /// Creates a Bearer authorization header value
        /// </summary>
        /// <param name="token">The bearer token</param>
        /// <returns>The formatted authorization header value</returns>
        public static string CreateBearerToken(string token)
        {
            return $"{BEARER_PREFIX}{token}";
        }
        
        /// <summary>
        /// Creates an API key authorization header value
        /// </summary>
        /// <param name="apiKey">The API key</param>
        /// <returns>The formatted authorization header value</returns>
        public static string CreateApiKeyToken(string apiKey)
        {
            return $"{API_KEY_PREFIX}{apiKey}";
        }
        
        #endregion
    }
}