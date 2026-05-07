using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Memoria.API.Models.Responses.Common;
using Memoria.API.Models.Responses.Player;
using Memoria.API.Models.Responses.Server;
using Memoria.API.Models.Requests.Player;
using Memoria.API.Models.Requests.User;
using Memoria.API.Models.Shared;
using UserModels = Memoria.API.Models.Responses.User;
using Memoria.API.Query.Player;
using Memoria.API.Services;
using Memoria.API.Client.Configuration;
using Memoria.API.Client;

namespace Memoria.API
{
    /// <summary>
    /// Modern, professional HTTP client for communicating with the MemoriaServer API.
    /// Uses dependency injection, proper configuration management, and modern patterns.
    /// Implements IDisposable for proper resource cleanup.
    /// </summary>
    public class ApiClient : IDisposable
    {
        private readonly IRestClient _restClient;
        private readonly Configuration _config;
        private readonly ILogger<ApiClient> _logger;
        private readonly ApiClientOptions _options;
        private readonly SemaphoreSlim _disposeSemaphore = new(1, 1);
        private bool _disposed = false;

        // Service instances for different API operations
        private readonly ServerStatusService _serverStatusService;
        private readonly PlayerDataService _playerDataService;
        private readonly UserAuthService _userAuthService;

        // Removed singleton pattern - ApiClient is now properly managed by dependency injection

        /// <summary>
        /// Legacy constructor for backward compatibility (especially tests).
        /// </summary>
        /// <param name="logger">Logger instance for API operations</param>
        public ApiClient(ILogger<ApiClient> logger) : this(logger, (IOptions<ApiClientOptions>?)null, (Configuration?)null)
        {
        }

        /// <summary>
        /// Initializes the API client with dependency injection and modern configuration patterns.
        /// </summary>
        /// <param name="logger">Logger instance for API operations</param>
        /// <param name="options">Strongly-typed configuration options</param>
        /// <param name="config">Legacy configuration for backward compatibility</param>
        public ApiClient(
            ILogger<ApiClient> logger,
            IOptions<ApiClientOptions>? options = null,
            Configuration? config = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options?.Value ?? CreateDefaultOptions();
            _config = config ?? throw new ArgumentNullException(nameof(config), "Configuration is required for ApiClient");

            // Validate configuration
            HttpClientConfiguration.ValidateOptions(_options);

            try
            {
                // Create RestClient with proper configuration
                _restClient = HttpClientConfiguration.CreateRestClient(_options);

                // Initialize specialized services. Cache wiring deliberately omitted: the
                // BackfillAvatarsLoop polls per-ContentId for changing data and would be
                // actively hurt by a stale-but-cached null response — see PersistenceContext
                // for the per-player cooldown that handles re-ask gating instead.
                _serverStatusService = new ServerStatusService(_restClient, _config, _logger);
                _playerDataService = new PlayerDataService(_restClient, _config, _logger);
                _userAuthService = new UserAuthService(_restClient, _config, _logger);

                // ApiClient is now properly managed by dependency injection - no singleton assignment needed

                _logger.LogInformation("ApiClient initialized successfully with base URL: {BaseUrl}", _options.BaseUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize ApiClient");
                throw;
            }
        }

        /// <summary>
        /// Creates default configuration options when none are provided
        /// </summary>
        /// <returns>Default ApiClientOptions</returns>
        private static ApiClientOptions CreateDefaultOptions()
        {
            return new ApiClientOptions
            {
                BaseUrl = "https://localhost:5001/v1/",
                TimeoutSeconds = 30,
                MaxRetryAttempts = 3,
                RetryDelayMilliseconds = 1000,
                EnableLogging = true,
                EnableDetailedErrorLogging = true
            };
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
        /// Checks if the MemoriaServer is online and responsive.
        /// Supports cancellation tokens for proper async handling.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the operation</param>
        /// <returns>Result indicating success or failure with error details</returns>
        public async Task<Result<bool>> CheckServerStatusAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            
            try
            {
                var result = await _serverStatusService.CheckServerStatusAsync();
                return Result<bool>.Success(result);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Server status check was cancelled");
                return Result<bool>.Failure("Operation was cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking server status");
                return Result<bool>.Failure("Failed to check server status", ex);
            }
        }

        /// <summary>
        /// Legacy method for backward compatibility
        /// </summary>
        public async Task<bool> CheckServerStatus()
        {
            var result = await CheckServerStatusAsync();
            return result.GetValueOrDefault(false);
        }
        
        /// <summary>
        /// Retrieves comprehensive server statistics with proper error handling.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the operation</param>
        /// <returns>Result containing server stats and status message</returns>
        public async Task<Result<(ServerStatsDto? ServerStats, string Message)>> CheckServerStatsAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            
            try
            {
                var result = await _serverStatusService.CheckServerStatsAsync();
                return Result<(ServerStatsDto?, string)>.Success(result);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Server stats check was cancelled");
                return Result<(ServerStatsDto?, string)>.Failure("Operation was cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking server stats");
                return Result<(ServerStatsDto?, string)>.Failure("Failed to check server stats", ex);
            }
        }

        /// <summary>
        /// Legacy method for backward compatibility
        /// </summary>
        public async Task<(ServerStatsDto? ServerStats, string Message)> CheckServerStats()
        {
            var result = await CheckServerStatsAsync();
            return result.GetValueOrDefault((null, "Failed to retrieve server stats"));
        }

        // ========== PLAYER DATA METHODS ==========
        
        /// <summary>
        /// Searches for players using the provided query parameters with modern error handling.
        /// </summary>
        /// <typeparam name="T">Type of player data to return</typeparam>
        /// <param name="query">Query object containing search parameters</param>
        /// <param name="cancellationToken">Cancellation token for the operation</param>
        /// <returns>Result containing paginated results and status message</returns>
        public async Task<Result<(PaginationBase<T>? Page, string Message)>> GetPlayersAsync<T>(
            PlayerQueryObject query, 
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            
            if (query == null)
                return Result<(PaginationBase<T>?, string)>.Failure("Query cannot be null");
            
            try
            {
                var result = await _playerDataService.GetPlayersAsync<T>(query);
                return Result<(PaginationBase<T>?, string)>.Success(result);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Player search was cancelled");
                return Result<(PaginationBase<T>?, string)>.Failure("Operation was cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching for players");
                return Result<(PaginationBase<T>?, string)>.Failure("Failed to search for players", ex);
            }
        }

        /// <summary>
        /// Legacy method for backward compatibility
        /// </summary>
        public async Task<(PaginationBase<T>? Page, string Message)> GetPlayers<T>(PlayerQueryObject query)
        {
            var result = await GetPlayersAsync<T>(query);
            return result.GetValueOrDefault((null, "Failed to retrieve players"));
        }

        /// <summary>
        /// Retrieves detailed information for a specific player with modern error handling.
        /// </summary>
        /// <param name="id">Player's Content ID</param>
        /// <param name="cancellationToken">Cancellation token for the operation</param>
        /// <returns>Result containing detailed player data and status message</returns>
        public async Task<Result<(PlayerDetailed? Player, string Message)>> GetPlayerByIdAsync(
            long id, 
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            
            if (id <= 0)
                return Result<(PlayerDetailed?, string)>.Failure("Player ID must be greater than 0");
            
            try
            {
                var result = await _playerDataService.GetPlayerByIdAsync(id);
                return Result<(PlayerDetailed?, string)>.Success(result);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Player lookup was cancelled for ID: {PlayerId}", id);
                return Result<(PlayerDetailed?, string)>.Failure("Operation was cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving player by ID: {PlayerId}", id);
                return Result<(PlayerDetailed?, string)>.Failure("Failed to retrieve player details", ex);
            }
        }

        /// <summary>
        /// Legacy method for backward compatibility
        /// </summary>
        public async Task<(PlayerDetailed? Player, string Message)> GetPlayerById(long id)
        {
            var result = await GetPlayerByIdAsync(id);
            return result.GetValueOrDefault((null, "Failed to retrieve player"));
        }

        /// <summary>
        /// Uploads a batch of player data with modern error handling.
        /// </summary>
        /// <param name="players">List of player data to upload</param>
        /// <param name="cancellationToken">Cancellation token for the operation</param>
        /// <returns>Result indicating success or failure</returns>
        public async Task<Result<bool>> PostPlayersAsync(
            List<PostPlayerRequest> players, 
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            
            if (players == null || players.Count == 0)
                return Result<bool>.Failure("Player list cannot be null or empty");
            
            try
            {
                var success = await _playerDataService.PostPlayersAsync(players);
                return Result<bool>.Success(success);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Player data upload was cancelled");
                return Result<bool>.Failure("Operation was cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading player data");
                return Result<bool>.Failure("Failed to upload player data", ex);
            }
        }

        /// <summary>
        /// Legacy method for backward compatibility
        /// </summary>
        public async Task<bool> PostPlayers(List<PostPlayerRequest> players)
        {
            var result = await PostPlayersAsync(players);
            return result.GetValueOrDefault(false);
        }

        /// <summary>
        /// Uploads player data with detailed error information and modern patterns.
        /// </summary>
        /// <param name="players">List of player data to upload</param>
        /// <param name="cancellationToken">Cancellation token for the operation</param>
        /// <returns>Result with success status and authentication failure indicator</returns>
        public async Task<Result<(bool Success, bool AuthenticationFailure)>> PostPlayersWithDetailsAsync(
            List<PostPlayerRequest> players, 
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            
            if (players == null || players.Count == 0)
                return Result<(bool, bool)>.Failure("Player list cannot be null or empty");
            
            try
            {
                var result = await _playerDataService.PostPlayersWithDetailsAsync(players);
                return Result<(bool, bool)>.Success(result);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Player data upload with details was cancelled");
                return Result<(bool, bool)>.Failure("Operation was cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading player data with details");
                return Result<(bool, bool)>.Failure("Failed to upload player data", ex);
            }
        }

        /// <summary>
        /// Legacy method for backward compatibility
        /// </summary>
        public async Task<(bool Success, bool AuthenticationFailure)> PostPlayersWithDetails(List<PostPlayerRequest> players)
        {
            var result = await PostPlayersWithDetailsAsync(players);
            return result.GetValueOrDefault((false, false));
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
        /// Initiates Discord OAuth authentication flow with modern error handling.
        /// </summary>
        /// <param name="register">User registration data</param>
        /// <param name="cancellationToken">Cancellation token for the operation</param>
        /// <returns>Result containing user data and status message</returns>
        public async Task<Result<(UserModels.User? User, string Message)>> DiscordAuthAsync(
            UserRegister register, 
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            
            if (register == null)
                return Result<(UserModels.User?, string)>.Failure("Registration data cannot be null");
            
            try
            {
                var result = await _userAuthService.DiscordAuthAsync(register);
                return Result<(UserModels.User?, string)>.Success(result);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Discord authentication was cancelled");
                return Result<(UserModels.User?, string)>.Failure("Operation was cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during Discord authentication");
                return Result<(UserModels.User?, string)>.Failure("Discord authentication failed", ex);
            }
        }

        /// <summary>
        /// Legacy method for backward compatibility
        /// </summary>
        public async Task<(UserModels.User? User, string Message)> DiscordAuth(UserRegister register)
        {
            var result = await DiscordAuthAsync(register);
            return result.GetValueOrDefault((null, "Discord authentication failed"));
        }
        
        /// <summary>
        /// Performs direct user login with modern error handling.
        /// </summary>
        /// <param name="loginUser">User login credentials</param>
        /// <param name="cancellationToken">Cancellation token for the operation</param>
        /// <returns>Result containing user data and status message</returns>
        public async Task<Result<(UserModels.User? User, string Message)>> UserLoginAsync(
            UserRegister loginUser, 
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            
            if (loginUser == null)
                return Result<(UserModels.User?, string)>.Failure("Login data cannot be null");
            
            try
            {
                var result = await _userAuthService.UserLoginAsync(loginUser);
                return Result<(UserModels.User?, string)>.Success(result);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("User login was cancelled");
                return Result<(UserModels.User?, string)>.Failure("Operation was cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during user login");
                return Result<(UserModels.User?, string)>.Failure("User login failed", ex);
            }
        }

        /// <summary>
        /// Legacy method for backward compatibility
        /// </summary>
        public async Task<(UserModels.User? User, string Message)> UserLogin(UserRegister loginUser)
        {
            var result = await UserLoginAsync(loginUser);
            return result.GetValueOrDefault((null, "User login failed"));
        }
        
        /// <summary>
        /// Updates user profile information with modern error handling.
        /// </summary>
        /// <param name="config">Updated user configuration data</param>
        /// <param name="cancellationToken">Cancellation token for the operation</param>
        /// <returns>Result containing updated user data and status message</returns>
        public async Task<Result<(UserModels.User? User, string Message)>> UserUpdateAsync(
            UserUpdateDto config, 
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            
            if (config == null)
                return Result<(UserModels.User?, string)>.Failure("Update data cannot be null");
            
            try
            {
                var result = await _userAuthService.UserUpdateAsync(config);
                return Result<(UserModels.User?, string)>.Success(result);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("User update was cancelled");
                return Result<(UserModels.User?, string)>.Failure("Operation was cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during user update");
                return Result<(UserModels.User?, string)>.Failure("User update failed", ex);
            }
        }

        /// <summary>
        /// Legacy method for backward compatibility
        /// </summary>
        public async Task<(UserModels.User? User, string Message)> UserUpdate(UserUpdateDto config)
        {
            var result = await UserUpdateAsync(config);
            return result.GetValueOrDefault((null, "User update failed"));
        }
        
        /// <summary>
        /// Refreshes the current user's profile information with modern error handling.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the operation</param>
        /// <returns>Result containing current user data and status message</returns>
        public async Task<Result<(UserModels.User? User, string Message)>> UserRefreshMyInfoAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            
            try
            {
                var result = await _userAuthService.UserRefreshMyInfoAsync();
                return Result<(UserModels.User?, string)>.Success(result);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("User info refresh was cancelled");
                return Result<(UserModels.User?, string)>.Failure("Operation was cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing user info");
                return Result<(UserModels.User?, string)>.Failure("Failed to refresh user info", ex);
            }
        }

        /// <summary>
        /// Legacy method for backward compatibility
        /// </summary>
        public async Task<(UserModels.User? User, string Message)> UserRefreshMyInfo()
        {
            var result = await UserRefreshMyInfoAsync();
            return result.GetValueOrDefault((null, "Failed to refresh user info"));
        }

        /// <summary>
        /// Generates a one-time code that the user pastes into the web app's /me/link page to
        /// merge their Discord identity onto this plugin install. Pass-through to UserAuthService;
        /// returns the raw ApiResponse so the UI can react to the 503 / 401 / etc. status codes.
        /// </summary>
        public async Task<ApiResponse<UserModels.LinkGenerateResponse>> GenerateWebLinkCodeAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            return await _userAuthService.GenerateWebLinkCodeAsync(cancellationToken);
        }

        /// <summary>
        /// Claims ownership of a Lodestone character profile with modern error handling.
        /// </summary>
        /// <param name="lodestoneProfileLink">URL to the Lodestone character profile</param>
        /// <param name="state">Claim state</param>
        /// <param name="cancellationToken">Cancellation token for the operation</param>
        /// <returns>Result containing claim result data and status message</returns>
        public async Task<Result<(UserModels.ClaimLodestoneCharacterDto? LodestoneProfile, string Message)>> ClaimLodestoneProfileAsync(
            string lodestoneProfileLink, 
            int state, 
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            
            if (string.IsNullOrWhiteSpace(lodestoneProfileLink))
                return Result<(UserModels.ClaimLodestoneCharacterDto?, string)>.Failure("Lodestone profile link cannot be null or empty");
            
            try
            {
                var result = await _userAuthService.ClaimLodestoneProfileAsync(lodestoneProfileLink, state);
                return Result<(UserModels.ClaimLodestoneCharacterDto?, string)>.Success(result);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Lodestone profile claim was cancelled");
                return Result<(UserModels.ClaimLodestoneCharacterDto?, string)>.Failure("Operation was cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error claiming Lodestone profile");
                return Result<(UserModels.ClaimLodestoneCharacterDto?, string)>.Failure("Failed to claim Lodestone profile", ex);
            }
        }

        /// <summary>
        /// Legacy method for backward compatibility
        /// </summary>
        public async Task<(UserModels.ClaimLodestoneCharacterDto? LodestoneProfile, string Message)> ClaimLodestoneProfile(string lodestoneProfileLink, int state)
        {
            var result = await ClaimLodestoneProfileAsync(lodestoneProfileLink, state);
            return result.GetValueOrDefault((null, "Failed to claim Lodestone profile"));
        }

        /// <summary>
        /// Throws an ObjectDisposedException if the client has been disposed
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ApiClient));
        }

        /// <summary>
        /// Disposes of the ApiClient and its resources
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Protected dispose method for proper disposal pattern
        /// </summary>
        /// <param name="disposing">True if disposing managed resources</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                _disposeSemaphore.Wait();
                try
                {
                    if (!_disposed)
                    {
                        _restClient?.Dispose();
                        _disposeSemaphore?.Dispose();
                        _disposed = true;
                        
                        // ApiClient is now properly managed by dependency injection - no singleton cleanup needed
                        
                        _logger?.LogDebug("ApiClient disposed");
                    }
                }
                finally
                {
                    if (!_disposed)
                        _disposeSemaphore.Release();
                }
            }
        }
    }
}