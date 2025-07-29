using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using AlphaScope.API.Models.Common;
using AlphaScope.API.Models.Player;
using AlphaScope.API.Query.Player;
using AlphaScope.Properties;

namespace AlphaScope.API.Services
{
    /// <summary>
    /// Service for managing player data operations
    /// </summary>
    public class PlayerDataService : IApiClientBase
    {
        public IRestClient RestClient { get; }
        public Configuration Config { get; }
        public ILogger Logger { get; }

        public PlayerDataService(IRestClient restClient, Configuration config, ILogger logger)
        {
            RestClient = restClient;
            Config = config;
            Logger = logger;
        }

        /// <summary>
        /// Searches for players using the provided query parameters
        /// </summary>
        public async Task<(PaginationBase<T>? Page, string Message)> GetPlayersAsync<T>(PlayerQueryObject query)
        {
            try
            {
                var request = new RestRequest("players")
                    .AddHeader("V", Utils.clientVer)
                    .AddHeader("L", Config.Language);

                AddQueryParameters(request, query);

                var response = await RestClient.ExecuteGetAsync(request).ConfigureAwait(false);

                if (response.IsSuccessful)
                {
                    var jsonResponse = JsonConvert.DeserializeObject<PaginationBase<T>>(response.Content!);
                    if (jsonResponse != null)
                    {
                        var message = $"- {Loc.ApiTotalFound} {jsonResponse.NextCount}";
                        return (jsonResponse, message);
                    }
                }

                var errorMessage = GetErrorMessage(response);
                return (null, errorMessage);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error while searching players");
                return (null, $"{Loc.ApiError} {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves detailed information for a specific player by their Content ID
        /// </summary>
        public async Task<(PlayerDetailed? Player, string Message)> GetPlayerByIdAsync(long id)
        {
            try
            {
                var request = new RestRequest($"players/{id}")
                    .AddHeader("V", Utils.clientVer)
                    .AddHeader("L", Config.Language);
                    
                var response = await RestClient.ExecuteGetAsync(request).ConfigureAwait(false);

                if (response.IsSuccessful)
                {
                    var player = JsonConvert.DeserializeObject<PlayerDetailed>(response.Content!);
                    return (player, "Player found.");
                }

                var errorMessage = GetErrorMessage(response);
                return (null, errorMessage);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error while retrieving player by ID: {PlayerId}", id);
                return (null, $"{Loc.ApiError} {ex.Message}");
            }
        }

        /// <summary>
        /// Uploads a batch of player data to the server
        /// </summary>
        public async Task<bool> PostPlayersAsync(List<PostPlayerRequest> players)
        {
            var result = await PostPlayersWithDetailsAsync(players);
            return result.Success;
        }

        /// <summary>
        /// Uploads a batch of player data to the server with detailed error information
        /// </summary>
        public async Task<(bool Success, bool AuthenticationFailure)> PostPlayersWithDetailsAsync(List<PostPlayerRequest> players)
        {
            try
            {
                var request = new RestRequest("players")
                    .AddHeader("V", Utils.clientVer)
                    .AddHeader("L", Config.Language);
                    
                request.AddJsonBody(players);
                var response = await RestClient.ExecutePostAsync(request).ConfigureAwait(false);
                
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    return (true, false);
                }
                
                Logger.LogWarning("Failed to post players data. Status: {StatusCode}", response.StatusCode);
                return (false, false);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error while posting players data");
                return (false, false);
            }
        }

        private static void AddQueryParameters(RestRequest request, PlayerQueryObject query)
        {
            if (!string.IsNullOrWhiteSpace(query.Name))
                request.AddQueryParameter("Name", query.Name, true);
                
            if (query.LocalContentId.HasValue && query.LocalContentId != 0)
                request.AddQueryParameter("LocalContentId", query.LocalContentId.ToString(), true);
                
            if (query.Cursor != 0)
                request.AddQueryParameter("Cursor", query.Cursor.ToString(), true);
                
            if (query.IsFetching != null)
                request.AddQueryParameter("IsFetching", query.IsFetching.ToString(), true);

            if (query.F_WorldIds != null && query.F_WorldIds.Any())
            {
                var worldIds = string.Join(",", query.F_WorldIds);
                request.AddQueryParameter("F_WorldIds", worldIds);
            }

            if (query.F_MatchAnyPartOfName != null)
                request.AddQueryParameter("F_MatchAnyPartOfName", query.F_MatchAnyPartOfName.ToString(), true);
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