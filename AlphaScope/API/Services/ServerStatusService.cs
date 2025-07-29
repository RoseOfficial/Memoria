using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RestSharp;
using System;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using AlphaScope.API.Models.Server;
using AlphaScope.Properties;

namespace AlphaScope.API.Services
{
    /// <summary>
    /// Service for managing server status checks and statistics
    /// </summary>
    public class ServerStatusService : IApiClientBase
    {
        public IRestClient RestClient { get; }
        public Configuration Config { get; }
        public ILogger Logger { get; }

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

        public ServerStatusService(IRestClient restClient, Configuration config, ILogger logger)
        {
            RestClient = restClient;
            Config = config;
            Logger = logger;
        }

        /// <summary>
        /// Checks if the AlphaScopeServer is online and responsive
        /// </summary>
        public async Task<bool> CheckServerStatusAsync()
        {
            try
            {
                IsCheckingServerStatus = true;

                var request = new RestRequest("server")
                    .AddHeader("V", Utils.clientVer)
                    .AddHeader("L", Config.Language);
                    
                var response = await RestClient.ExecuteGetAsync(request).ConfigureAwait(false);
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
                    return true;
                }
                
                ServerStatus = GetErrorMessage(response);
                return false;
            }
            catch (HttpRequestException ex)
            {
                Logger.LogWarning(ex, "Network error while checking server status");
                ServerStatus = $"{Loc.ApiError} Network connection failed";
                LastPingValue = -1;
                return false;
            }
            catch (TaskCanceledException ex)
            {
                Logger.LogWarning(ex, "Timeout while checking server status");
                ServerStatus = $"{Loc.ApiError} Request timed out";
                LastPingValue = -1;
                return false;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Unexpected error while checking server status");
                ServerStatus = $"{Loc.ApiError} {ex.Message}";
                LastPingValue = -1;
                return false;
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
            try
            {
                var request = new RestRequest("server/stats")
                    .AddHeader("V", Utils.clientVer)
                    .AddHeader("L", Config.Language);
                    
                var response = await RestClient.ExecuteGetAsync(request).ConfigureAwait(false);

                if (response.IsSuccessful)
                {
                    var serverStats = JsonConvert.DeserializeObject<ServerStatsDto>(response.Content!);
                    if (serverStats != null)
                    {
                        var message = Loc.ApiStatsRefreshed;
                        LastServerStats = (serverStats, message);
                        return (serverStats, message);
                    }
                }

                var errorMessage = GetErrorMessage(response);
                return (null, errorMessage);
            }
            catch (HttpRequestException ex)
            {
                Logger.LogWarning(ex, "Network error while fetching server stats");
                return (null, $"{Loc.ApiError} Network connection failed");
            }
            catch (JsonException ex)
            {
                Logger.LogError(ex, "Failed to parse server stats response");
                return (null, $"{Loc.ApiError} Invalid server response");
            }
            catch (TaskCanceledException ex)
            {
                Logger.LogWarning(ex, "Timeout while fetching server stats");
                return (null, $"{Loc.ApiError} Request timed out");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Unexpected error while fetching server stats");
                return (null, $"{Loc.ApiError} {ex.Message}");
            }
        }

        private static string GetErrorMessage(RestResponse response)
        {
            if (!string.IsNullOrWhiteSpace(response.Content))
                return $"{Loc.ApiError} {response.Content}";
            if (response.StatusCode == 0)
                return $"{Loc.ApiError} {Loc.ApiServiceUnavailable}";
            return $"{Loc.ApiError} {response.StatusCode}";
        }
    }
}