namespace Memoria.API.Constants
{
    /// <summary>
    /// Contains all API endpoint constants used throughout the Memoria API services.
    /// This centralizes endpoint management and reduces the risk of typos in URLs.
    /// </summary>
    public static class ApiEndpoints
    {
        #region Player Endpoints
        
        /// <summary>
        /// Base endpoint for player operations
        /// </summary>
        public const string PLAYERS = "players";
        
        /// <summary>
        /// Endpoint to get a specific player by ID
        /// Format: players/{contentId}
        /// </summary>
        public static string GetPlayerById(long contentId) => $"{PLAYERS}/{contentId}";
        
        #endregion
        
        #region Server Endpoints
        
        /// <summary>
        /// Base endpoint for server status operations
        /// </summary>
        public const string SERVER = "server";
        
        /// <summary>
        /// Endpoint for server statistics
        /// </summary>
        public const string SERVER_STATS = "server/stats";
        
        #endregion
        
        #region User Endpoints
        
        /// <summary>
        /// Base endpoint for user operations
        /// </summary>
        public const string USERS = "users";
        
        /// <summary>
        /// Endpoint for user login operations
        /// </summary>
        public const string USERS_LOGIN = "users/login";
        
        /// <summary>
        /// Endpoint for user profile updates
        /// </summary>
        public const string USERS_UPDATE = "users/update";
        
        /// <summary>
        /// Endpoint for getting current user information
        /// </summary>
        public const string USERS_ME = "users/me";
        
        /// <summary>
        /// Endpoint for claiming Lodestone character profiles
        /// </summary>
        public const string USERS_LODESTONE_CLAIM = "users/lodestone/claim";
        
        #endregion
        
        #region Authentication Endpoints
        
        /// <summary>
        /// Base path for authentication operations (relative to API root)
        /// </summary>
        public const string AUTH_BASE = "Auth";
        
        /// <summary>
        /// Discord OAuth authentication endpoint
        /// </summary>
        public const string DISCORD_AUTH = "Auth/DiscordAuth";
        
        /// <summary>
        /// Endpoint for waiting for login completion
        /// </summary>
        public const string WAIT_FOR_LOGIN = "waitforlogin";

        /// <summary>
        /// Endpoint for generating a one-time code that the web app redeems to link the
        /// caller's plugin install to its Discord-authenticated account.
        /// </summary>
        public const string AUTH_LINK_GENERATE = "auth/link/generate";

        #endregion
        
        #region Health and Monitoring Endpoints
        
        /// <summary>
        /// Health check endpoint for API connectivity testing
        /// </summary>
        public const string HEALTH = "health";
        
        #endregion
        
        #region URL Manipulation Helpers
        
        /// <summary>
        /// Standard API version prefix
        /// </summary>
        public const string API_VERSION_PREFIX = "v1/";
        
        /// <summary>
        /// Converts a v1 API URL to an Auth URL by replacing the version prefix
        /// </summary>
        /// <param name="baseUrl">The base API URL (e.g., "https://api.example.com/v1/")</param>
        /// <param name="endpoint">The authentication endpoint</param>
        /// <returns>The complete authentication URL</returns>
        public static string ToAuthUrl(string baseUrl, string endpoint)
        {
            var authBaseUrl = baseUrl.Replace(API_VERSION_PREFIX, string.Empty);
            return $"{authBaseUrl}{endpoint}";
        }
        
        /// <summary>
        /// Converts a v1 API URL to a non-versioned URL by removing the version prefix
        /// </summary>
        /// <param name="baseUrl">The base API URL (e.g., "https://api.example.com/v1/")</param>
        /// <returns>The base URL without version prefix</returns>
        public static string ToNonVersionedUrl(string baseUrl)
        {
            return baseUrl.Replace(API_VERSION_PREFIX, string.Empty);
        }
        
        #endregion
    }
}