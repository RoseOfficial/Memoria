using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RestSharp;
using System;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using AlphaScope.API.Abstractions.Services;
using AlphaScope.API.Abstractions.Cache;
using AlphaScope.API.Models.Responses.Server;
using AlphaScope.API.Models.Shared;
using AlphaScope.API.Constants;
using AlphaScope.API.Services.Cache;
using AlphaScope.Properties;

namespace AlphaScope.API.Services
{
    /// <summary>
    /// Service for managing server status checks and statistics
    /// </summary>
    public class ServerStatusService : BaseApiService, IServerStatusService
    {
        private readonly IApiCacheService? _cacheService;

        /// <summary>
        /// Current server status message
        /// </summary>
        public string ServerStatus { get; private set; } = string.Empty;
        
        /// <summary>
        /// Flag indicating if a server status check is in progress
        /// </summary>
        public bool IsCheckingServerStatus { get; private set; } = false;
        
        /// <summary>
        /// Last ping response time in milliseconds
        /// </summary>
        public long LastPingValue { get; private set; } = -1;
        
        /// <summary>
        /// Cached server statistics
        /// </summary>
        public (ServerStatsDto? ServerStats, string Message) LastServerStats { get; private set; } = new();

        public ServerStatusService(IRestClient restClient, Configuration config, ILogger logger, IApiCacheService? cacheService = null)
            : base(restClient, config, logger)
        {
            _cacheService = cacheService;
        }

        /// <summary>
        /// Checks if the AlphaScopeServer is online and responsive
        /// </summary>
        public async Task<bool> CheckServerStatusAsync()
        {
            var result = await CheckServerStatusAsync(CancellationToken.None);
            return result.Success && result.Data;
        }

        /// <summary>
        /// Checks if the AlphaScopeServer is online and responsive with cancellation support
        /// </summary>
        public async Task<ApiResponse<bool>> CheckServerStatusAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                IsCheckingServerStatus = true;

                var request = new RestRequest(ApiEndpoints.SERVER)
                    .AddHeader(ApiHeaders.VERSION, Utils.clientVer)
                    .AddHeader(ApiHeaders.LANGUAGE, Config.Language);
                    
                var response = await RestClient.ExecuteGetAsync(request, cancellationToken).ConfigureAwait(false);
                long pingValue = -1;
                
                // Perform network ping to measure latency
                using (var ping = new Ping())
                {
                    var uri = new Uri(Config.BaseUrl);
                    var reply = ping.Send(uri.Host, 1000);
                    pingValue = reply.RoundtripTime;
                }

                if (response.IsSuccessful)
                {
                    ServerStatus = "ONLINE";
                    LastPingValue = pingValue;
                    return ApiResponse<bool>.Ok(true, (int)response.StatusCode);
                }
                
                ServerStatus = GetErrorMessage(response);
                return ApiResponse<bool>.Ok(false, (int)response.StatusCode);
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                Logger.LogWarning(ex, "Timeout while checking server status");
                ServerStatus = $"{Loc.ApiError} {ErrorCodes.REQUEST_TIMEOUT_MESSAGE}";
                LastPingValue = -1;
                return ApiResponse<bool>.Fail($"{Loc.ApiError} {ErrorCodes.REQUEST_TIMEOUT_MESSAGE}", ErrorCodes.REQUEST_TIMEOUT);
            }
            catch (HttpRequestException ex)
            {
                Logger.LogWarning(ex, "Network error while checking server status");
                ServerStatus = $"{Loc.ApiError} {ErrorCodes.NETWORK_CONNECTION_FAILED_MESSAGE}";
                LastPingValue = -1;
                return ApiResponse<bool>.Fail($"{Loc.ApiError} {ErrorCodes.NETWORK_CONNECTION_FAILED_MESSAGE}", ErrorCodes.SERVICE_UNAVAILABLE);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Unexpected error while checking server status");
                ServerStatus = $"{Loc.ApiError} {ex.Message}";
                LastPingValue = -1;
                return HandleCommonException<bool>(ex, "checking server status");
            }
            finally
            {
                IsCheckingServerStatus = false;
            }
        }

        /// <summary>
        /// Retrieves comprehensive server statistics
        /// </summary>
        public async Task<(ServerStatsDto? ServerStats, string Message)> CheckServerStatsAsync()
        {
            var result = await CheckServerStatsAsync(CancellationToken.None);
            if (result.Success)
            {
                var message = Loc.ApiStatsRefreshed;
                LastServerStats = (result.Data, message);
                return (result.Data, message);
            }
            return (null, result.Error ?? "Failed to retrieve server stats");
        }

        /// <summary>
        /// Retrieves comprehensive server statistics with cancellation support
        /// </summary>
        public async Task<ApiResponse<ServerStatsDto>> CheckServerStatsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // Try to get from cache first if caching is available
                if (_cacheService != null)
                {
                    var cacheKey = ApiCacheService.GenerateServerStatsCacheKey();
                    var cachedResult = await _cacheService.GetAsync<ServerStatsDto>(cacheKey, cancellationToken);
                    
                    if (cachedResult.Success && cachedResult.Data != null)
                    {
                        Logger.LogDebug("Server stats retrieved from cache");
                        var message = Loc.ApiStatsRefreshed;
                        LastServerStats = (cachedResult.Data, message);
                        return ApiResponse<ServerStatsDto>.Ok(cachedResult.Data, 200);
                    }
                }

                var request = new RestRequest(ApiEndpoints.SERVER_STATS)
                    .AddHeader(ApiHeaders.VERSION, Utils.clientVer)
                    .AddHeader(ApiHeaders.LANGUAGE, Config.Language);
                    
                var response = await RestClient.ExecuteGetAsync(request, cancellationToken).ConfigureAwait(false);

                if (response.IsSuccessful)
                {
                    var serverStats = JsonConvert.DeserializeObject<ServerStatsDto>(response.Content!);
                    if (serverStats != null)
                    {
                        // Cache the result if caching is available
                        if (_cacheService != null)
                        {
                            var cacheKey = ApiCacheService.GenerateServerStatsCacheKey();
                            var cacheExpiry = DateTime.UtcNow.AddMinutes(5); // Cache server stats for 5 minutes
                            await _cacheService.SetAsync(cacheKey, serverStats, cacheExpiry, TimeSpan.FromMinutes(2), cancellationToken);
                            Logger.LogDebug("Server stats cached for 5 minutes");
                        }

                        var message = Loc.ApiStatsRefreshed;
                        LastServerStats = (serverStats, message);
                        return ApiResponse<ServerStatsDto>.Ok(serverStats, (int)response.StatusCode);
                    }
                }

                var errorMessage = GetErrorMessage(response);
                return ApiResponse<ServerStatsDto>.Fail(errorMessage, (int)response.StatusCode);
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                Logger.LogWarning(ex, "Timeout while fetching server stats");
                return ApiResponse<ServerStatsDto>.Fail($"{Loc.ApiError} {ErrorCodes.REQUEST_TIMEOUT_MESSAGE}", ErrorCodes.REQUEST_TIMEOUT);
            }
            catch (HttpRequestException ex)
            {
                Logger.LogWarning(ex, "Network error while fetching server stats");
                return ApiResponse<ServerStatsDto>.Fail($"{Loc.ApiError} {ErrorCodes.NETWORK_CONNECTION_FAILED_MESSAGE}", ErrorCodes.SERVICE_UNAVAILABLE);
            }
            catch (JsonException ex)
            {
                Logger.LogError(ex, "Failed to parse server stats response");
                return ApiResponse<ServerStatsDto>.Fail($"{Loc.ApiError} {ErrorCodes.INVALID_SERVER_RESPONSE_MESSAGE}", ErrorCodes.UNPROCESSABLE_ENTITY);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Unexpected error while fetching server stats");
                return HandleCommonException<ServerStatsDto>(ex, "fetching server stats");
            }
        }

        // GetErrorMessage method is inherited from BaseApiService

        /// <summary>
        /// Performs a lightweight ping test to measure network latency
        /// </summary>
        public async Task<ApiResponse<long>> PingServerAsync(int timeoutMs = 5000, CancellationToken cancellationToken = default)
        {
            try
            {
                if (timeoutMs <= 0)
                    return ApiResponse<long>.Fail(ErrorCodes.TIMEOUT_MUST_BE_GREATER_THAN_ZERO_MESSAGE, ErrorCodes.BAD_REQUEST);

                using var ping = new Ping();
                var uri = new Uri(Config.BaseUrl);
                var reply = await ping.SendPingAsync(uri.Host, timeoutMs);
                
                if (reply.Status == IPStatus.Success)
                {
                    return ApiResponse<long>.Ok(reply.RoundtripTime);
                }
                
                return ApiResponse<long>.Ok(-1);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error performing ping test");
                return HandleCommonException<long>(ex, "performing ping test");
            }
        }

        #region Overridden BaseApiService Methods

        /// <summary>
        /// Creates service diagnostics with server status-specific information
        /// </summary>
        protected override ServiceDiagnostics CreateServiceDiagnostics()
        {
            return new ServiceDiagnostics
            {
                IsHealthy = !string.IsNullOrWhiteSpace(Config.BaseUrl),
                Status = ServerStatus,
                ServiceStartTime = DateTime.UtcNow.AddHours(-1), // Placeholder
                ConfigurationValid = !string.IsNullOrWhiteSpace(Config.BaseUrl),
                LastError = ServerStatus.Contains("ERROR") ? ServerStatus : null
            };
        }

        #endregion
    }
}