using Microsoft.Extensions.Logging;
using RestSharp;
using RestSharp.Serializers.NewtonsoftJson;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AlphaScope.API.Models.Common;
using AlphaScope.API.Models.Player;
using AlphaScope.API.Models.Server;
using UserModels = AlphaScope.API.Models.User;
using AlphaScope.API.Query.Player;
using AlphaScope.API.Services;

namespace AlphaScope.API
{
    /// <summary>
    /// Main HTTP client for communicating with the AlphaScopeServer API.
    /// Coordinates between specialized service classes for different API operations.
    /// Uses RestSharp for HTTP operations and Newtonsoft.Json for serialization.
    /// </summary>
    public class ApiClient
    {
        private static IRestClient _restClient = new RestClient();
        private readonly Configuration _config = Plugin.Instance?.Configuration ?? new Configuration();
        private readonly ILogger<ApiClient> _logger;

        // Service instances for different API operations
        private readonly ServerStatusService _serverStatusService;
        private readonly PlayerDataService _playerDataService;
        private readonly UserAuthService _userAuthService;

        /// <summary>
        /// Singleton instance of the API client for global access
        /// </summary>
        internal static ApiClient Instance { get; private set; } = null!;

        /// <summary>
        /// Initializes the API client with logging and configures the RestSharp client.
        /// Sets up the base URL from configuration and configures JSON serialization.
        /// Creates specialized service instances for different API operations.
        /// </summary>
        /// <param name="logger">Logger instance for API operations</param>
        public ApiClient(ILogger<ApiClient> logger)
        {
            _logger = logger;
            
            // Configure RestClient with base URL if valid, otherwise use default
            if (Uri.IsWellFormedUriString(_config.BaseUrl, UriKind.Absolute))
            {
                var options = new RestClientOptions(_config.BaseUrl);
                _restClient = new RestClient(options, configureSerialization: s => s.UseNewtonsoftJson());
            }
            else
            {
                _restClient = new RestClient(configureSerialization: s => s.UseNewtonsoftJson());
            }

            // Initialize specialized services
            _serverStatusService = new ServerStatusService(_restClient, _config, _logger);
            _playerDataService = new PlayerDataService(_restClient, _config, _logger);
            _userAuthService = new UserAuthService(_restClient, _config, _logger);
            
            Instance = this;
        }

        // ========== SERVER STATUS PROPERTIES ==========
        
        /// <summary>
        /// Current server status message ("ONLINE", error message, etc.)
        /// </summary>
        public string ServerStatus => _serverStatusService.ServerStatus;
        
        /// <summary>
        /// Flag indicating if a server status check is currently in progress
        /// </summary>
        public bool IsCheckingServerStatus => _serverStatusService.IsCheckingServerStatus;
        
        /// <summary>
        /// Last ping response time in milliseconds (-1 if unavailable)
        /// </summary>
        public long LastPingValue => _serverStatusService.LastPingValue;
        
        /// <summary>
        /// Cached server statistics and last status message
        /// </summary>
        public (ServerStatsDto? ServerStats, string Message) LastServerStats => _serverStatusService.LastServerStats;

        // ========== SERVER STATUS METHODS ==========
        
        /// <summary>
        /// Checks if the AlphaScopeServer is online and responsive.
        /// Performs both an HTTP request to the server endpoint and a network ping.
        /// Updates server status and ping values for display in the UI.
        /// </summary>
        /// <returns>True if server is online and responding, false otherwise</returns>
        public async Task<bool> CheckServerStatus()
        {
            return await _serverStatusService.CheckServerStatusAsync();
        }
        
        /// <summary>
        /// Retrieves comprehensive server statistics including user counts, data metrics, and system info.
        /// Caches the results for display in the UI.
        /// </summary>
        /// <returns>Tuple containing server stats DTO and status message</returns>
        public async Task<(ServerStatsDto? ServerStats, string Message)> CheckServerStats()
        {
            return await _serverStatusService.CheckServerStatsAsync();
        }

        // ========== PLAYER DATA METHODS ==========
        
        /// <summary>
        /// Searches for players using the provided query parameters.
        /// Supports pagination, filtering by world, name matching, and content ID lookup.
        /// Generic method that can return PlayerDto or PlayerSearchDto based on server response.
        /// </summary>
        /// <typeparam name="T">Type of player data to return (PlayerDto or PlayerSearchDto)</typeparam>
        /// <param name="query">Query object containing search parameters and filters</param>
        /// <returns>Tuple containing paginated results and status message</returns>
        public async Task<(PaginationBase<T>? Page, string Message)> GetPlayers<T>(PlayerQueryObject query)
        {
            return await _playerDataService.GetPlayersAsync<T>(query);
        }

        /// <summary>
        /// Retrieves detailed information for a specific player by their Content ID.
        /// Returns comprehensive player data including customization, job info, and metadata.
        /// </summary>
        /// <param name="id">Player's Content ID (LocalContentId)</param>
        /// <returns>Tuple containing detailed player data and status message</returns>
        public async Task<(PlayerDetailed? Player, string Message)> GetPlayerById(long id)
        {
            return await _playerDataService.GetPlayerByIdAsync(id);
        }

        /// <summary>
        /// Uploads a batch of player data to the server.
        /// Used by the persistence system to sync locally collected player information.
        /// </summary>
        /// <param name="players">List of player data to upload</param>
        /// <returns>True if upload was successful, false otherwise</returns>
        public async Task<bool> PostPlayers(List<PostPlayerRequest> players)
        {
            return await _playerDataService.PostPlayersAsync(players);
        }

        /// <summary>
        /// Uploads a batch of player data to the server with detailed error information.
        /// Used by the persistence system to sync locally collected player information and detect authentication failures.
        /// </summary>
        /// <param name="players">List of player data to upload</param>
        /// <returns>Tuple with success status and authentication failure indicator</returns>
        public async Task<(bool Success, bool AuthenticationFailure)> PostPlayersWithDetails(List<PostPlayerRequest> players)
        {
            return await _playerDataService.PostPlayersWithDetailsAsync(players);
        }

        // ========== USER AUTHENTICATION PROPERTIES ==========
        
        /// <summary>
        /// Flag indicating if Discord OAuth login is currently in progress
        /// </summary>
        public bool IsLoggingIn => _userAuthService.IsLoggingIn;
        
        /// <summary>
        /// Generated authentication URL for Discord OAuth flow
        /// </summary>
        public string authUrl => _userAuthService.AuthUrl;

        // ========== USER AUTHENTICATION METHODS ==========
        
        /// <summary>
        /// Initiates Discord OAuth authentication flow.
        /// Opens the user's browser to Discord OAuth page and waits for login completion.
        /// Uses Server-Sent Events to receive authentication status updates.
        /// </summary>
        /// <param name="register">User registration data for the OAuth flow</param>
        /// <returns>Tuple containing user data (if successful) and status message</returns>
        public async Task<(UserModels.User? User, string Message)> DiscordAuth(UserModels.UserRegister register)
        {
            return await _userAuthService.DiscordAuthAsync(register);
        }
        
        /// <summary>
        /// Performs direct user login using username/password or API key authentication.
        /// Alternative to Discord OAuth for users who prefer direct login.
        /// </summary>
        /// <param name="loginUser">User login credentials</param>
        /// <returns>Tuple containing user data (if successful) and status message</returns>
        public async Task<(UserModels.User? User, string Message)> UserLogin(UserModels.UserRegister loginUser)
        {
            return await _userAuthService.UserLoginAsync(loginUser);
        }
        
        /// <summary>
        /// Updates user profile information on the server.
        /// Used to sync local configuration changes with the server-side user profile.
        /// </summary>
        /// <param name="config">Updated user configuration data</param>
        /// <returns>Tuple containing updated user data and status message</returns>
        public async Task<(UserModels.User? User, string Message)> UserUpdate(UserModels.UserUpdateDto config)
        {
            return await _userAuthService.UserUpdateAsync(config);
        }
        
        /// <summary>
        /// Refreshes the current user's profile information from the server.
        /// Used to sync server-side changes back to the local client.
        /// </summary>
        /// <returns>Tuple containing current user data and status message</returns>
        public async Task<(UserModels.User? User, string Message)> UserRefreshMyInfo()
        {
            return await _userAuthService.UserRefreshMyInfoAsync();
        }

        /// <summary>
        /// Claims ownership of a Lodestone character profile.
        /// Links the user's AlphaScope account to their official FFXIV Lodestone character page.
        /// Supports verification codes and multi-step claim processes.
        /// </summary>
        /// <param name="lodestoneProfileLink">URL to the Lodestone character profile</param>
        /// <param name="state">Claim state (0=initiate, 1=verify, etc.)</param>
        /// <returns>Tuple containing claim result data and status message</returns>
        public async Task<(UserModels.ClaimLodestoneCharacterDto? LodestoneProfile, string Message)> ClaimLodestoneProfile(string lodestoneProfileLink, int state)
        {
            return await _userAuthService.ClaimLodestoneProfileAsync(lodestoneProfileLink, state);
        }
    }
}