using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using AlphaScope.API.Abstractions.Services;
using AlphaScope.API.Abstractions.Cache;
using AlphaScope.API.Models.Responses.Common;
using AlphaScope.API.Models.Responses.Player;
using AlphaScope.API.Models.Requests.Player;
using AlphaScope.API.Models.Shared;
using AlphaScope.API.Query.Player;
using AlphaScope.API.Constants;
using AlphaScope.API.Services.Cache;
using AlphaScope.Properties;

namespace AlphaScope.API.Services
{
    /// <summary>
    /// Service for managing player data operations
    /// </summary>
    public class PlayerDataService : BaseApiService, IPlayerDataService
    {
        private readonly IApiCacheService? _cacheService;

        public PlayerDataService(IRestClient restClient, Configuration config, ILogger logger, IApiCacheService? cacheService = null)
            : base(restClient, config, logger)
        {
            _cacheService = cacheService;
        }

        /// <summary>
        /// Searches for players using the provided query parameters
        /// </summary>
        public async Task<(PaginationBase<T>? Page, string Message)> GetPlayersAsync<T>(PlayerQueryObject query)
        {
            var result = await GetPlayersAsync<T>(query, CancellationToken.None);
            if (result.Success)
            {
                var message = result.Data != null ? $"- {Loc.ApiTotalFound} {result.Data.NextCount}" : "No results found";
                return (result.Data, message);
            }
            return (null, result.Error ?? "Unknown error occurred");
        }

        /// <summary>
        /// Searches for players using the provided query parameters with cancellation support
        /// </summary>
        public async Task<ApiResponse<PaginationBase<T>>> GetPlayersAsync<T>(PlayerQueryObject query, CancellationToken cancellationToken = default)
        {
            try
            {
                // Generate cache key for player search
                var worldIds = query.F_WorldIds != null && query.F_WorldIds.Any() 
                    ? string.Join(",", query.F_WorldIds) 
                    : string.Empty;
                var cacheKey = ApiCacheService.GeneratePlayerSearchCacheKey(query.Name, query.Cursor, worldIds);
                
                // Try to get from cache first if caching is available
                if (_cacheService != null)
                {
                    var cachedResult = await _cacheService.GetAsync<PaginationBase<T>>(cacheKey, cancellationToken);
                    
                    if (cachedResult.Success && cachedResult.Data != null)
                    {
                        Logger.LogDebug("Player search results retrieved from cache for key: {CacheKey}", cacheKey);
                        return ApiResponse<PaginationBase<T>>.Ok(cachedResult.Data, 200);
                    }
                }

                var request = new RestRequest(ApiEndpoints.PLAYERS)
                    .AddHeader(ApiHeaders.VERSION, Utils.clientVer)
                    .AddHeader(ApiHeaders.LANGUAGE, Config.Language);

                AddQueryParameters(request, query);

                var response = await RestClient.ExecuteGetAsync(request, cancellationToken).ConfigureAwait(false);

                if (response.IsSuccessful)
                {
                    var jsonResponse = JsonConvert.DeserializeObject<PaginationBase<T>>(response.Content!);
                    if (jsonResponse != null)
                    {
                        // Cache the search results if caching is available
                        if (_cacheService != null)
                        {
                            var cacheExpiry = DateTime.UtcNow.AddMinutes(10); // Cache search results for 10 minutes
                            await _cacheService.SetAsync(cacheKey, jsonResponse, cacheExpiry, TimeSpan.FromMinutes(5), cancellationToken);
                            Logger.LogDebug("Player search results cached for key: {CacheKey}", cacheKey);
                        }
                        
                        return ApiResponse<PaginationBase<T>>.Ok(jsonResponse, (int)response.StatusCode);
                    }
                }

                var errorMessage = GetErrorMessage(response);
                return ApiResponse<PaginationBase<T>>.Fail(errorMessage, (int)response.StatusCode);
            }
            catch (Exception ex)
            {
                return HandleCommonException<PaginationBase<T>>(ex, "searching players");
            }
        }

        /// <summary>
        /// Retrieves detailed information for a specific player by their Content ID
        /// </summary>
        public async Task<(PlayerDetailed? Player, string Message)> GetPlayerByIdAsync(long id)
        {
            var result = await GetPlayerByIdAsync(id, CancellationToken.None);
            if (result.Success)
            {
                return (result.Data, "Player found.");
            }
            return (null, result.Error ?? "Player not found");
        }

        /// <summary>
        /// Retrieves detailed information for a specific player by their Content ID with cancellation support
        /// </summary>
        public async Task<ApiResponse<PlayerDetailed>> GetPlayerByIdAsync(long contentId, CancellationToken cancellationToken = default)
        {
            try
            {
                // Try to get from cache first if caching is available
                if (_cacheService != null)
                {
                    var cacheKey = ApiCacheService.GeneratePlayerCacheKey(contentId);
                    var cachedResult = await _cacheService.GetAsync<PlayerDetailed>(cacheKey, cancellationToken);
                    
                    if (cachedResult.Success && cachedResult.Data != null)
                    {
                        Logger.LogDebug("Player details retrieved from cache for ID: {ContentId}", contentId);
                        return ApiResponse<PlayerDetailed>.Ok(cachedResult.Data, 200);
                    }
                }

                var request = new RestRequest(ApiEndpoints.GetPlayerById(contentId))
                    .AddHeader(ApiHeaders.VERSION, Utils.clientVer)
                    .AddHeader(ApiHeaders.LANGUAGE, Config.Language);
                    
                var response = await RestClient.ExecuteGetAsync(request, cancellationToken).ConfigureAwait(false);

                if (response.IsSuccessful)
                {
                    var player = JsonConvert.DeserializeObject<PlayerDetailed>(response.Content!);
                    if (player != null)
                    {
                        // Cache the player details if caching is available
                        if (_cacheService != null)
                        {
                            var cacheKey = ApiCacheService.GeneratePlayerCacheKey(contentId);
                            var cacheExpiry = DateTime.UtcNow.AddMinutes(30); // Cache player details for 30 minutes
                            await _cacheService.SetAsync(cacheKey, player, cacheExpiry, TimeSpan.FromMinutes(15), cancellationToken);
                            Logger.LogDebug("Player details cached for ID: {ContentId}", contentId);
                        }
                        
                        return ApiResponse<PlayerDetailed>.Ok(player, (int)response.StatusCode);
                    }
                }

                var errorMessage = GetErrorMessage(response);
                return ApiResponse<PlayerDetailed>.Fail(errorMessage, (int)response.StatusCode);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error while retrieving player by ID: {PlayerId}", contentId);
                return HandleCommonException<PlayerDetailed>(ex, "retrieving player by ID");
            }
        }

        /// <summary>
        /// Uploads a batch of player data to the server
        /// </summary>
        public async Task<bool> PostPlayersAsync(List<PostPlayerRequest> players)
        {
            var result = await PostPlayersAsync((IEnumerable<PostPlayerRequest>)players, CancellationToken.None);
            return result.Success;
        }

        /// <summary>
        /// Uploads a batch of player data to the server with cancellation support
        /// </summary>
        public async Task<ApiResponse> PostPlayersAsync(IEnumerable<PostPlayerRequest> players, CancellationToken cancellationToken = default)
        {
            var result = await PostPlayersWithDetailsAsync(players, cancellationToken);
            return result.Success ? ApiResponse.Ok() : ApiResponse.Fail(result.Error ?? "Upload failed", result.StatusCode);
        }

        /// <summary>
        /// Uploads a batch of player data to the server with detailed error information
        /// </summary>
        public async Task<(bool Success, bool AuthenticationFailure)> PostPlayersWithDetailsAsync(List<PostPlayerRequest> players)
        {
            var result = await PostPlayersWithDetailsAsync((IEnumerable<PostPlayerRequest>)players, CancellationToken.None);
            return result.Success ? result.Data : (false, false);
        }

        /// <summary>
        /// Uploads a batch of player data to the server with detailed error information and cancellation support
        /// </summary>
        public async Task<ApiResponse<(bool Success, bool AuthenticationFailure)>> PostPlayersWithDetailsAsync(IEnumerable<PostPlayerRequest> players, CancellationToken cancellationToken = default)
        {
            try
            {
                if (players == null)
                    return ApiResponse<(bool Success, bool AuthenticationFailure)>.Fail(ErrorCodes.PARAMETER_CANNOT_BE_NULL_MESSAGE, ErrorCodes.BAD_REQUEST);

                var playersList = players.ToList();
                if (!playersList.Any())
                    return ApiResponse<(bool Success, bool AuthenticationFailure)>.Fail(ErrorCodes.COLLECTION_CANNOT_BE_EMPTY_MESSAGE, ErrorCodes.BAD_REQUEST);

                var request = new RestRequest(ApiEndpoints.PLAYERS)
                    .AddHeader(ApiHeaders.VERSION, Utils.clientVer)
                    .AddHeader(ApiHeaders.LANGUAGE, Config.Language);
                    
                request.AddJsonBody(playersList);
                var response = await RestClient.ExecutePostAsync(request, cancellationToken).ConfigureAwait(false);
                
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    return ApiResponse<(bool Success, bool AuthenticationFailure)>.Ok((true, false), (int)response.StatusCode);
                }
                
                var isAuthFailure = response.StatusCode == HttpStatusCode.Unauthorized;
                Logger.LogWarning("Failed to post players data. Status: {StatusCode}", response.StatusCode);
                return ApiResponse<(bool Success, bool AuthenticationFailure)>.Ok((false, isAuthFailure), (int)response.StatusCode);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error while posting players data");
                return HandleCommonException<(bool Success, bool AuthenticationFailure)>(ex, "posting players data");
            }
        }

        // IApiClientBase implementation is inherited from BaseApiService

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

        // GetErrorMessage method is inherited from BaseApiService
    }
}