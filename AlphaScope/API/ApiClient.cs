// FFXIV client structure dependencies
using FFXIVClientStructs.FFXIV.Common.Lua;

// Microsoft framework dependencies
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.Logging;

// Third-party HTTP and JSON libraries
using Newtonsoft.Json;
using RestSharp;
using RestSharp.Serializers.NewtonsoftJson;

// AlphaScope internal dependencies
using AlphaScope.API.Models;
using AlphaScope.API.Query;
using AlphaScope.GUI;
using AlphaScope.Handlers;
using AlphaScope.Properties;

// System dependencies
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace AlphaScope.API
{
    /// <summary>
    /// HTTP client for communicating with the AlphaScopeServer API.
    /// Handles authentication, request/response processing, error handling, and data serialization.
    /// Provides methods for server status, player data, retainer data, and user management.
    /// Uses RestSharp for HTTP operations and Newtonsoft.Json for serialization.
    /// </summary>
    public class ApiClient
    {
        /// <summary>
        /// RestSharp client for making HTTP requests to the server
        /// </summary>
        public static IRestClient _restClient = new RestClient();
        
        /// <summary>
        /// Plugin configuration containing API settings and authentication data
        /// </summary>
        public Configuration Config = Plugin.Instance?.Configuration ?? new Configuration();
        
        /// <summary>
        /// Logger for API operations and error reporting
        /// </summary>
        private readonly ILogger<ApiClient> _logger;
        
        /// <summary>
        /// Singleton instance of the API client for global access
        /// </summary>
        internal static ApiClient Instance { get; private set; } = null!;
        /// <summary>
        /// Initializes the API client with logging and configures the RestSharp client.
        /// Sets up the base URL from configuration and configures JSON serialization.
        /// </summary>
        /// <param name="logger">Logger instance for API operations</param>
        public ApiClient(ILogger<ApiClient> logger)
        {
            _logger = logger;
            
            // Configure RestClient with base URL if valid, otherwise use default
            if (Uri.IsWellFormedUriString(Config.BaseUrl, UriKind.Absolute))
            {
                var options = new RestClientOptions(Config.BaseUrl);
                _restClient = new RestClient(options, configureSerialization: s => s.UseNewtonsoftJson());
            }
            else
            {
                _restClient = new RestClient(configureSerialization: s => s.UseNewtonsoftJson());
            }
            Instance = this;
        }

        // ========== SERVER STATUS MANAGEMENT ==========
        
        /// <summary>
        /// Current server status message ("ONLINE", error message, etc.)
        /// </summary>
        public string _ServerStatus = string.Empty;
        
        /// <summary>
        /// Flag indicating if a server status check is currently in progress
        /// </summary>
        public bool IsCheckingServerStatus = false;
        
        /// <summary>
        /// Last ping response time in milliseconds (-1 if unavailable)
        /// </summary>
        public long _LastPingValue = -1;

        /// <summary>
        /// Checks if the AlphaScopeServer is online and responsive.
        /// Performs both an HTTP request to the server endpoint and a network ping.
        /// Updates server status and ping values for display in the UI.
        /// </summary>
        /// <returns>True if server is online and responding, false otherwise</returns>
        public async Task<bool> CheckServerStatus()
        {
            try
            {
                IsCheckingServerStatus = true;

                // Make HTTP request to server status endpoint
                var request = new RestRequest($"server")
                    .AddHeader("api-key", Token)
                    .AddHeader("V", Utils.clientVer)
                    .AddHeader("L", Config.Language);
                var response = await _restClient.ExecuteGetAsync(request).ConfigureAwait(false);
                long pingValue = -1;
                
                // Perform network ping to measure latency
                using (Ping pp = new Ping())
                {
                    Uri uri = new Uri($"{Config.BaseUrl}");
                    PingReply reply = pp.Send(uri.Host, 1000);

                    pingValue = reply.RoundtripTime;
                }

                // Process server response
                if (response.IsSuccessful)
                {
                    _ServerStatus = "ONLINE"; 
                    _LastPingValue = pingValue;
                    IsCheckingServerStatus = false;
                    return true;
                }
                else if (!string.IsNullOrWhiteSpace(response.Content))
                    _ServerStatus = $"{Loc.ApiError} {response.Content}";
                else if (response.StatusCode == 0)
                    _ServerStatus = $"{Loc.ApiError} {Loc.ApiServiceUnavailable}";
                else
                    _ServerStatus = $"{Loc.ApiError} {response.StatusCode.ToString()}";

                IsCheckingServerStatus = false;
                return false;
            }
            // Handle specific HTTP and network errors
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "Network error while checking server status");
                _ServerStatus = $"{Loc.ApiError} Network connection failed"; 
                _LastPingValue = -1;
                IsCheckingServerStatus = false; 
                return false;
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogWarning(ex, "Timeout while checking server status");
                _ServerStatus = $"{Loc.ApiError} Request timed out"; 
                _LastPingValue = -1;
                IsCheckingServerStatus = false; 
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while checking server status");
                _ServerStatus = $"{Loc.ApiError} {ex.Message}"; 
                _LastPingValue = -1;
                IsCheckingServerStatus = false; 
                return false;
            }
        }
        /// <summary>
        /// Cached server statistics and last status message
        /// </summary>
        public (ServerStatsDto ServerStats, string Message) _LastServerStats = new();
        
        /// <summary>
        /// Retrieves comprehensive server statistics including user counts, data metrics, and system info.
        /// Caches the results for display in the UI.
        /// </summary>
        /// <returns>Tuple containing server stats DTO and status message</returns>
        public async Task<(ServerStatsDto ServerStats, string Message)> CheckServerStats()
        {
            string Message = string.Empty;
            try
            {
                var request = new RestRequest($"server/stats").AddHeader("api-key", Token).AddHeader("V", Utils.clientVer).AddHeader("L", Config.Language);
                var response = await _restClient.ExecuteGetAsync(request).ConfigureAwait(false);

                if (response.IsSuccessful)
                {
                    var _JsonResponse = JsonConvert.DeserializeObject<ServerStatsDto>(response.Content!);
                    if (_JsonResponse != null)
                    {
                        Message = Loc.ApiStatsRefreshed;
                        _LastServerStats = (_JsonResponse, Message);
                        return (_JsonResponse, Message);
                    }
                }
                else if (!string.IsNullOrWhiteSpace(response.Content))
                    Message = $"{Loc.ApiError} {response.Content}";
                else if (response.StatusCode == 0)
                    Message = $"{Loc.ApiError} {Loc.ApiServiceUnavailable}";
                else
                    Message = $"{Loc.ApiError} {response.StatusCode.ToString()}";

                return (null, Message);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "Network error while fetching server stats");
                Message = $"{Loc.ApiError} Network connection failed";
                return (null, Message);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse server stats response");
                Message = $"{Loc.ApiError} Invalid server response";
                return (null, Message);
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogWarning(ex, "Timeout while fetching server stats");
                Message = $"{Loc.ApiError} Request timed out";
                return (null, Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while fetching server stats");
                Message = $"{Loc.ApiError} {ex.Message}";
                return (null, Message);
            }
        }

        /// <summary>
        /// Cached player and retainer count statistics with last status message
        /// </summary>
        public (ServerPlayerAndRetainerStatsDto Stats, string Message) LastPlayerAndRetainerCountStats = new();
        
        /// <summary>
        /// Retrieves focused statistics about player and retainer counts on the server.
        /// Used for dashboard displays and data overview.
        /// </summary>
        /// <returns>Tuple containing player/retainer stats DTO and status message</returns>
        public async Task<(ServerPlayerAndRetainerStatsDto PlayerAndRetainerStats, string Message)> GetPlayerAndRetainerCountStats()
        {
            string Message = string.Empty;
            try
            {
                var request = new RestRequest($"server/stats/players-retainers").AddHeader("api-key", Token).AddHeader("V", Utils.clientVer).AddHeader("L", Config.Language);
                var response = await _restClient.ExecuteGetAsync(request).ConfigureAwait(false);

                if (response.IsSuccessful)
                {
                    var _JsonResponse = JsonConvert.DeserializeObject<ServerPlayerAndRetainerStatsDto>(response.Content!);
                    if (_JsonResponse != null)
                    {
                        Message = Loc.ApiStatsRefreshed;
                        LastPlayerAndRetainerCountStats = (_JsonResponse, Message);
                        return (_JsonResponse, Message);
                    }
                }
                else if (!string.IsNullOrWhiteSpace(response.Content))
                    Message = $"{Loc.ApiError} {response.Content}";
                else if (response.StatusCode == 0)
                    Message = $"{Loc.ApiError} {Loc.ApiServiceUnavailable}";
                else
                    Message = $"{Loc.ApiError} {response.StatusCode.ToString()}";

                return (null, Message);
            }
            catch (Exception ex)
            {
                Message = $"{Loc.ApiError} {ex.Message}";
                return (null, Message);
            }
        }
        // ========== PLAYER DATA MANAGEMENT ==========
        
        /// <summary>
        /// Authentication token for API requests, combining API key and account ID
        /// </summary>
        public string Token => $"{Config.Key}-{Config.AccountId}";
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
            var _GetPlayerSearchResult = new Dictionary<long, PlayerDto>();
            string Message = string.Empty;
            try
            {
                var request = new RestRequest($"players").AddHeader("api-key", Token).AddHeader("V", Utils.clientVer).AddHeader("L", Config.Language);
                if (!string.IsNullOrWhiteSpace(query.Name))
                    request.AddQueryParameter("Name", query.Name, true);
                if (!string.IsNullOrWhiteSpace(query.LocalContentId.ToString()))
                    request.AddQueryParameter("LocalContentId", query.LocalContentId.ToString(), true);
                if (!string.IsNullOrWhiteSpace(query.Cursor.ToString()))
                    request.AddQueryParameter("Cursor", query.Cursor.ToString(), true);
                if (!string.IsNullOrWhiteSpace(query.IsFetching.ToString()))
                    request.AddQueryParameter("IsFetching", query.IsFetching.ToString(), true);

                if (query.F_WorldIds != null && query.F_WorldIds.Any())
                {
                    var worldIds = string.Join(",", query.F_WorldIds);
                    request.AddQueryParameter("F_WorldIds", worldIds);
                }

                if (!string.IsNullOrWhiteSpace(query.F_MatchAnyPartOfName.ToString()))
                    request.AddQueryParameter("F_MatchAnyPartOfName", query.F_MatchAnyPartOfName.ToString(), true);

                var response = await _restClient.ExecuteGetAsync(request).ConfigureAwait(false);

                if (response.IsSuccessful)
                {
                    var jsonResponse = JsonConvert.DeserializeObject<PaginationBase<T>>(response.Content!);
                    if (jsonResponse != null)
                    {
                        Message = $"- {Loc.ApiTotalFound} {jsonResponse.NextCount}";
                        return (jsonResponse, Message);
                    }
                }
                else if (!string.IsNullOrWhiteSpace(response.Content))
                    Message = $"{Loc.ApiError} {response.Content}";
                else if (response.StatusCode == 0)
                    Message = $"{Loc.ApiError} {Loc.ApiServiceUnavailable}";
                else
                    Message = $"{Loc.ApiError} {response.StatusCode.ToString()}";

                return (null, Message);
            }
            catch (Exception ex)
            {
                Message = $"{Loc.ApiError} {ex.Message}";
                return (null, Message);
            }
        }

        /// <summary>
        /// Retrieves detailed information for a specific player by their Content ID.
        /// Returns comprehensive player data including customization, job info, and metadata.
        /// </summary>
        /// <param name="id">Player's Content ID (LocalContentId)</param>
        /// <returns>Tuple containing detailed player data and status message</returns>
        public async Task<(PlayerDetailed Player, string Message)> GetPlayerById(long id)
        {
            string Message = string.Empty;
            try
            {
                var request = new RestRequest($"players/{id}").AddHeader("api-key", Token).AddHeader("V", Utils.clientVer).AddHeader("L", Config.Language);
                var response = await _restClient.ExecuteGetAsync(request).ConfigureAwait(false);

                if (response.IsSuccessful)
                {
                    var _JsonResponse = JsonConvert.DeserializeObject<PlayerDetailed>(response.Content!);
                    Message = "Player found.";
                    return (_JsonResponse, Message);
                }
                else if (!string.IsNullOrWhiteSpace(response.Content))
                    Message = $"{Loc.ApiError} {response.Content}";
                else if (response.StatusCode == 0)
                    Message = $"{Loc.ApiError} {Loc.ApiServiceUnavailable}";
                else
                    Message = $"{Loc.ApiError} {response.StatusCode.ToString()}";

                return (null, Message);
            }
            catch (Exception ex)
            {
                Message = $"{Loc.ApiError} {ex.Message}";
                return (null, Message);
            }
        }

        /// <summary>
        /// Uploads a batch of player data to the server.
        /// Used by the persistence system to sync locally collected player information.
        /// </summary>
        /// <param name="players">List of player data to upload</param>
        /// <returns>True if upload was successful, false otherwise</returns>
        public async Task<bool> PostPlayers(List<PostPlayerRequest> players)
        {
            try
            {
                var request = new RestRequest($"players").AddHeader("api-key", Token).AddHeader("V", Utils.clientVer).AddHeader("L", Config.Language);
                request.AddJsonBody(players);
                var response = await _restClient.ExecutePostAsync(request).ConfigureAwait(false);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                return false;
            }
        }
        // ========== RETAINER DATA MANAGEMENT ==========
        
        /// <summary>
        /// Searches for retainers using the provided query parameters.
        /// Supports pagination, filtering by world, name matching, and owner lookup.
        /// Generic method that can return RetainerDto or RetainerSearchDto based on server response.
        /// </summary>
        /// <typeparam name="T">Type of retainer data to return (RetainerDto or RetainerSearchDto)</typeparam>
        /// <param name="query">Query object containing search parameters and filters</param>
        /// <returns>Tuple containing paginated results and status message</returns>
        public async Task<(PaginationBase<T>? Page, string Message)> GetRetainers<T>(RetainerQueryObject query)
        {
            string Message = string.Empty;
            try
            {
                var request = new RestRequest($"retainers").AddHeader("api-key", Token).AddHeader("V", Utils.clientVer).AddHeader("L", Config.Language); ;
                if (!string.IsNullOrWhiteSpace(query.Name))
                    request.AddQueryParameter("Name", query.Name, true);
                if (!string.IsNullOrWhiteSpace(query.Cursor.ToString()))
                    request.AddQueryParameter("Cursor", query.Cursor.ToString(), true);
                if (!string.IsNullOrWhiteSpace(query.IsFetching.ToString()))
                    request.AddQueryParameter("IsFetching", query.IsFetching.ToString(), true);

                if (query.F_WorldIds != null && query.F_WorldIds.Any())
                {
                    var worldIds = string.Join(",", query.F_WorldIds);
                    request.AddQueryParameter("F_WorldIds", worldIds);
                }

                if (!string.IsNullOrWhiteSpace(query.F_MatchAnyPartOfName.ToString()))
                    request.AddQueryParameter("F_MatchAnyPartOfName", query.F_MatchAnyPartOfName.ToString(), true);

                var response = await _restClient.ExecuteGetAsync(request).ConfigureAwait(false);
                if (response.IsSuccessful)
                {
                    var jsonResponse = JsonConvert.DeserializeObject<PaginationBase<T>>(response.Content!);
                    if (jsonResponse != null)
                    {
                        Message = $"- {Loc.ApiTotalFound} {jsonResponse.NextCount}";
                        return (jsonResponse, Message);
                    }
                }
                else if (!string.IsNullOrWhiteSpace(response.Content))
                    Message = $"{Loc.ApiError} {response.Content}";
                else if (response.StatusCode == 0)
                    Message = $"{Loc.ApiError} {Loc.ApiServiceUnavailable}";
                else
                    Message = $"{Loc.ApiError} {response.StatusCode.ToString()}";

                return (null, Message);
            }
            catch (Exception ex)
            {
                Message = $"{Loc.ApiError} {ex.Message}";
                return (null, Message);
            }
        }
        /// <summary>
        /// Uploads a batch of retainer data to the server.
        /// Used by the persistence system to sync locally collected retainer information.
        /// </summary>
        /// <param name="retainers">List of retainer data to upload</param>
        /// <returns>True if upload was successful, false otherwise</returns>
        public async Task<bool> PostRetainers(List<PostRetainerRequest> retainers)
        {
            try
            {
                var request = new RestRequest($"retainers").AddHeader("api-key", Token).AddHeader("V", Utils.clientVer).AddHeader("L", Config.Language);
                request.AddJsonBody(retainers);
                var response = await _restClient.ExecutePostAsync(request).ConfigureAwait(false);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                return false;
            }
        }
        // ========== USER AUTHENTICATION AND MANAGEMENT ==========
        
        /// <summary>
        /// Dedicated HTTP client for Discord OAuth authentication flow
        /// </summary>
        private static readonly HttpClient _httpClient = new HttpClient();
        
        /// <summary>
        /// Flag indicating if Discord OAuth login is currently in progress
        /// </summary>
        public bool IsLoggingIn = false;
        
        /// <summary>
        /// Generated authentication URL for Discord OAuth flow
        /// </summary>
        public string authUrl = string.Empty;
        /// <summary>
        /// Initiates Discord OAuth authentication flow.
        /// Opens the user's browser to Discord OAuth page and waits for login completion.
        /// Uses Server-Sent Events to receive authentication status updates.
        /// </summary>
        /// <param name="register">User registration data for the OAuth flow</param>
        /// <returns>Tuple containing user data (if successful) and status message</returns>
        public async Task<(User? User, string Message)> DiscordAuth(UserRegister register)
        {
            IsLoggingIn = true;
            string Message = string.Empty;
            try
            {
                string output = JsonConvert.SerializeObject(register);
                byte[] bytes = Encoding.UTF8.GetBytes(output);
                string data = System.Convert.ToBase64String(bytes);
                authUrl = Config.BaseUrl.Replace("v1/", "Auth/DiscordAuth?") + data;

                Utils.TryOpenURI(new Uri(authUrl));

                var response = await _httpClient.GetAsync($"{Config.BaseUrl.Replace("v1/", "")}waitforlogin?data={data}", HttpCompletionOption.ResponseHeadersRead);
                using (var stream = await response.Content.ReadAsStreamAsync())
                using (var reader = new StreamReader(stream))
                {
                    while (IsLoggingIn)
                    {
                        var line = await reader.ReadLineAsync();
                        if (line == null) break;
                        if (line.StartsWith("data:"))
                        {
                            var message = line.Substring("data:".Length).Trim();
                            if (message.Contains("Login successful"))
                            {
                                IsLoggingIn = false;
                                SettingsWindow.Instance.RefreshUserProfileInfo();
                                break;
                            }
                        }
                    }
                }

                return (null, Message);
            }
            catch (Exception ex)
            {
                Message = $"{Loc.ApiError} {ex.Message}";
                return (null, Message);
            }
        }
        /// <summary>
        /// Performs direct user login using username/password or API key authentication.
        /// Alternative to Discord OAuth for users who prefer direct login.
        /// </summary>
        /// <param name="loginUser">User login credentials</param>
        /// <returns>Tuple containing user data (if successful) and status message</returns>
        public async Task<(User? User, string Message)> UserLogin(UserRegister loginUser)
        {
            string Message = string.Empty;
            try
            {
                var request = new RestRequest($"users/login");
                request.AddJsonBody(loginUser);

                var response = await _restClient.ExecutePostAsync(request).ConfigureAwait(true);

                if (response.IsSuccessful)
                {
                    var _JsonResponse = JsonConvert.DeserializeObject<User>(response.Content!);
                    if (_JsonResponse != null)
                    {
                        Message = "Logged in successfully.";
                        return (_JsonResponse, Message);
                    }
                }
                else if (!string.IsNullOrWhiteSpace(response.Content))
                    Message = $"{Loc.ApiError} {response.Content}";
                else if (response.StatusCode == 0)
                    Message = $"{Loc.ApiError} {Loc.ApiServiceUnavailable}";
                else
                    Message = $"{Loc.ApiError} {response.StatusCode.ToString()}";

                return (null, Message);
            }
            catch (Exception ex)
            {
                Message = $"{Loc.ApiError} {ex.Message}";
                return (null, Message);
            }
        }
        /// <summary>
        /// Updates user profile information on the server.
        /// Used to sync local configuration changes with the server-side user profile.
        /// </summary>
        /// <param name="config">Updated user configuration data</param>
        /// <returns>Tuple containing updated user data and status message</returns>
        public async Task<(User? User, string Message)> UserUpdate(UserUpdateDto config)
        {
            string Message = string.Empty;
            try
            {
                var request = new RestRequest($"users/update").AddHeader("api-key", Token).AddHeader("V", Utils.clientVer).AddHeader("L", Config.Language);
                request.AddJsonBody(config);
                var response = await _restClient.ExecutePostAsync(request).ConfigureAwait(true);

                if (response.IsSuccessful && !string.IsNullOrWhiteSpace(response.Content))
                {
                    var _JsonResponse = JsonConvert.DeserializeObject<User>(response.Content!);
                    if (_JsonResponse != null)
                    {
                        Message = Loc.StConfigSaved;
                        return (_JsonResponse, Message);
                    }
                }
                else if (!string.IsNullOrWhiteSpace(response.Content))
                    Message = $"{Loc.ApiError} {response.Content}";
                else if (response.StatusCode == 0)
                    Message = $"{Loc.ApiError} {Loc.ApiServiceUnavailable}";
                else
                    Message = $"{Loc.ApiError} {response.StatusCode.ToString()}";

                return (null, Message);
            }
            catch (Exception ex)
            {
                Message = $"{Loc.ApiError} {ex.Message}";
                return (null, Message);
            }
        }
        /// <summary>
        /// Refreshes the current user's profile information from the server.
        /// Used to sync server-side changes back to the local client.
        /// </summary>
        /// <returns>Tuple containing current user data and status message</returns>
        public async Task<(User? User, string Message)> UserRefreshMyInfo()
        {
            string Message = string.Empty;
            try
            {
                var request = new RestRequest($"users/me").AddHeader("api-key", Token).AddHeader("V", Utils.clientVer).AddHeader("L", Config.Language);
                var response = await _restClient.ExecuteGetAsync(request).ConfigureAwait(true);

                if (response.IsSuccessful)
                {
                    var _JsonResponse = JsonConvert.DeserializeObject<User>(response.Content!);
                    if (_JsonResponse != null)
                    {
                        Message = Loc.ApiProfileRefreshed;
                        return (_JsonResponse,Message);
                    }
                }
                else if (!string.IsNullOrWhiteSpace(response.Content))
                    Message = $"{Loc.ApiError} {response.Content}";
                else if (response.StatusCode == 0)
                    Message = $"{Loc.ApiError} {Loc.ApiServiceUnavailable}";
                else
                    Message = $"{Loc.ApiError} {response.StatusCode.ToString()}"; 

                return (null, Message);
            }
            catch (Exception ex)
            {
                Message = $"{Loc.ApiError} {ex.Message}";
                return (null, Message);
            }
        }

        /// <summary>
        /// Claims ownership of a Lodestone character profile.
        /// Links the user's AlphaScope account to their official FFXIV Lodestone character page.
        /// Supports verification codes and multi-step claim processes.
        /// </summary>
        /// <param name="lodestoneProfileLink">URL to the Lodestone character profile</param>
        /// <param name="state">Claim state (0=initiate, 1=verify, etc.)</param>
        /// <returns>Tuple containing claim result data and status message</returns>
        public async Task<(ClaimLodestoneCharacterDto? LodestoneProfile, string Message)> ClaimLodestoneProfile(string lodestoneProfileLink, int state)
        {
            string Message = string.Empty;
            try
            {
                var request = new RestRequest($"users/lodestone/claim").AddHeader("api-key", Token).AddHeader("V", Utils.clientVer).AddHeader("L", Config.Language);
                request.AddQueryParameter("url", lodestoneProfileLink);
                request.AddQueryParameter("state", state);
                var response = await _restClient.ExecutePostAsync(request).ConfigureAwait(true);

                if (response.IsSuccessful)
                {
                    var _JsonResponse = JsonConvert.DeserializeObject<ClaimLodestoneCharacterDto>(response.Content!);
                    if (_JsonResponse != null)
                    {
                        Message = _JsonResponse.Message;
                        return (_JsonResponse, Message);
                    }
                }
                else if (!string.IsNullOrWhiteSpace(response.Content))
                    Message = $"{Loc.ApiError} {response.Content}";
                else if (response.StatusCode == 0)
                    Message = $"{Loc.ApiError} {Loc.ApiServiceUnavailable}";
                else
                    Message = $"{Loc.ApiError} {response.StatusCode.ToString()}";

                return (null, Message);
            }
            catch (Exception ex)
            {
                Message = $"{Loc.ApiError} {ex.Message}";
                return (null, Message);
            }
        }
    }
}
