// System dependencies
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

// Dalamud framework dependencies
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Network.Structures;
using Dalamud.Plugin.Services;

// FFXIV client structure dependencies
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Fate;
using FFXIVClientStructs.FFXIV.Common.Lua;

// Third-party UI and data dependencies
using ImGuiNET;
using Lumina.Excel.Sheets;

// Microsoft framework dependencies
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

// AlphaScope internal dependencies
using AlphaScope.API;
using AlphaScope.API.Models;
using AlphaScope.Database;
using AlphaScope.GUI;
using AlphaScope.Services;

// Static imports for specific functionality
using static FFXIVClientStructs.Havok.Animation.Deform.Skinning.hkaMeshBinding;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;
using static AlphaScope.Handlers.PersistenceContext;

namespace AlphaScope.Handlers;

/// <summary>
/// Core data persistence and synchronization handler for AlphaScope.
/// Manages local data caching, background data uploads to server, and coordination between
/// local database storage and server API synchronization. Handles player data
/// lifecycle from initial discovery through upload and caching.
/// </summary>
internal sealed class PersistenceContext
{
    /// <summary>
    /// Logger for persistence operations and debugging
    /// </summary>
    public static ILogger<PersistenceContext> _logger;
    
    /// <summary>
    /// Dalamud client state service for accessing current player information
    /// </summary>
    public static IClientState _clientState;
    
    /// <summary>
    /// Service provider for dependency injection and database access
    /// </summary>
    public static IServiceProvider _serviceProvider;
    
    
    /// <summary>
    /// Cache of player data indexed by Content ID for fast access
    /// </summary>
    public static readonly ConcurrentDictionary<ulong, CachedPlayer> _playerCache = new();
    
    /// <summary>
    /// Cache mapping Account ID to list of Content IDs (for alt character tracking)
    /// </summary>
    public static readonly ConcurrentDictionary<ulong, List<ulong>> _AccountIdCache = new();

    /// <summary>
    /// Queue of player data pending upload to server
    /// </summary>
    public static ConcurrentDictionary<ulong, PostPlayerRequest> _UploadPlayers = new();
    
    /// <summary>
    /// Cache of player data that has already been successfully uploaded to server
    /// </summary>
    public static ConcurrentDictionary<ulong, PostPlayerRequest> _UploadedPlayersCache = new();
    
    /// <summary>
    /// Legacy placeholder - Retainer upload queue (functionality removed)
    /// </summary>
    public static ConcurrentDictionary<ulong, object> _UploadRetainers = new();
    
    /// <summary>
    /// Legacy placeholder - Uploaded retainer cache (functionality removed)
    /// </summary>
    public static ConcurrentDictionary<ulong, object> _UploadedRetainersCache = new();

    /// <summary>
    /// Cache of recently scanned players with scan timestamps for avoiding duplicate processing
    /// </summary>
    public static ConcurrentDictionary<ulong, (CachedPlayer Player, long ScannedAt)> _recentlyScannedPlayers = new();
    
    /// <summary>
    /// Cleans up old entries from the recently scanned players cache to prevent memory bloat.
    /// Removes players that were scanned more than the specified time ago.
    /// </summary>
    /// <param name="maxAgeInHours">Maximum age in hours before a cached entry is removed (default: 24)</param>
    public static void CleanupOldRecentPlayers(int maxAgeInHours = 24)
    {
        var cutoffTime = DateTimeOffset.UtcNow.AddHours(-maxAgeInHours).ToUnixTimeSeconds();
        var keysToRemove = _recentlyScannedPlayers
            .Where(kvp => kvp.Value.ScannedAt < cutoffTime)
            .Select(kvp => kvp.Key)
            .ToList();
            
        foreach (var key in keysToRemove)
        {
            _recentlyScannedPlayers.TryRemove(key, out _);
        }
    }


    /// <summary>
    /// Singleton instance of the persistence context
    /// </summary>
    private static PersistenceContext _instance = null;
    
    /// <summary>
    /// Gets the singleton instance of the persistence context
    /// </summary>
    public static PersistenceContext Instance
    {
        get
        {
            return _instance;
        }
    }

    /// <summary>
    /// Initializes the persistence context with required services and starts background upload processing.
    /// Sets up singleton instance, initializes caches from database, and begins continuous data upload loop.
    /// </summary>
    /// <param name="logger">Logger for persistence operations</param>
    /// <param name="clientState">Dalamud client state service</param>
    /// <param name="serviceProvider">Service provider for dependency injection</param>
    /// <param name="data">Dalamud data manager service</param>
    public PersistenceContext(ILogger<PersistenceContext> logger, IClientState clientState,
        IServiceProvider serviceProvider, IDataManager data)
    {
        if (_instance == null)
        {
            _instance = this;
        }

        _logger = logger;
        _clientState = clientState;
        _serviceProvider = serviceProvider;

        // Force clear all static caches immediately on startup
        _logger.LogInformation("PersistenceContext: Force clearing all static caches on startup");
        _playerCache.Clear();
        _AccountIdCache.Clear();
        _UploadPlayers.Clear();
        _UploadedPlayersCache.Clear();
        _recentlyScannedPlayers.Clear();
        _logger.LogInformation($"PersistenceContext: After force clear - playerCache has {_playerCache.Count} entries");

        // Load existing data from database into memory caches
        _logger.LogInformation("PersistenceContext: Calling ReloadCache() during initialization...");
        ReloadCache();

        // Background upload processing disabled for debugging
        _cancellationTokenSource = new CancellationTokenSource();
        _logger.LogInformation("PersistenceContext: Server upload disabled for debugging");
    }
    /// <summary>
    /// Clears all in-memory caches. Useful when database is reset or when fresh start is needed.
    /// </summary>
    public static void ClearCache()
    {
        _logger?.LogInformation("Clearing all in-memory caches...");
        _playerCache.Clear();
        _AccountIdCache.Clear();
        _UploadPlayers.Clear();
        _UploadedPlayersCache.Clear();
        _recentlyScannedPlayers.Clear();
        _logger?.LogInformation("All caches cleared successfully");
    }

    /// <summary>
    /// Reloads all in-memory caches from the local SQLite database.
    /// This method rebuilds player caches from persistent storage.
    /// Called during initialization and when cache invalidation is needed.
    /// </summary>
    public static void ReloadCache()
    {
        try
        {
            // Clear existing cache first to ensure fresh state
            ClearCache();
            
            using (IServiceScope scope = _serviceProvider.CreateScope())
            {
                using var dbContext = scope.ServiceProvider.GetRequiredService<RetainerTrackContext>();

                var playerCount = 0;
                var avatarCount = 0;
                _logger?.LogInformation("Starting cache reload from database...");
                
                foreach (var player in dbContext.Players)
                {
                    _playerCache[player.LocalContentId] = new CachedPlayer
                    {
                        AccountId = player.AccountId,
                        Name = player.Name ?? string.Empty,
                        AvatarLink = player.AvatarLink,
                    };
                    playerCount++;
                    if (!string.IsNullOrEmpty(player.AvatarLink))
                    {
                        avatarCount++;
                        _logger?.LogInformation($"[AVATAR DEBUG] Loaded player {player.Name} with avatar: {player.AvatarLink}");
                    }
                }
                _logger?.LogInformation($"Cache reload completed: loaded {playerCount} players, {avatarCount} with avatars");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during cache reload from database");
        }
    }


   public static void UpdateAccountIds()
    {
        foreach (var player in _playerCache)
        {
            if (player.Value.AccountId != null)
            {
                var _GetAccountsCache = _AccountIdCache.TryGetValue((ulong)player.Value.AccountId, out var AccountContentIds);
                if (_GetAccountsCache )
                {
                    if (!AccountContentIds.Contains(player.Key))
                    {
                        AccountContentIds.Add(player.Key);
                    }
                }
                else
                {
                    _AccountIdCache[(ulong)player.Value.AccountId] = new List<ulong> { player.Key };
                }
            }
        }
    }

    private const int CacheExpirationTimeInSeconds = 215; // 3.30 minutes
    public static bool AnamnesisFound;

    private static bool HasDataChanged<T>(T request, T cachedRequest) where T : class
    {
        switch (request)
        {
            case PostPlayerRequest playerRequest when cachedRequest is PostPlayerRequest cachedPlayer:
                return playerRequest.Name != cachedPlayer.Name ||
                       (playerRequest.AccountId.HasValue && !cachedPlayer.AccountId.HasValue) ||
                       playerRequest.TerritoryId != cachedPlayer.TerritoryId ||
                       playerRequest.HomeWorldId != cachedPlayer.HomeWorldId ||
                       playerRequest.CurrentWorldId != cachedPlayer.CurrentWorldId;

            default:
                throw new InvalidOperationException("Unsupported type for data change check");
        }
    }

    private static void UpdateCacheIfNeeded<T>(
        ulong id,
        T request,
        ConcurrentDictionary<ulong, T> uploadList,
        ConcurrentDictionary<ulong, T> cache
    ) where T : class
    {
        if (cache.TryGetValue(id, out var cachedRequest))
        {
            if (Tools.UnixTime - GetCreatedAt(cachedRequest) > CacheExpirationTimeInSeconds)
            {
                cache.TryRemove(id, out _);
            }
            else if (!HasDataChanged(request, cachedRequest))
            {
                return;
            }
        }
        uploadList[id] = request;
        cache[id] = request;
    }
    private static int GetCreatedAt<T>(T request)
    {
        return request switch
        {
            PostPlayerRequest player => player.CreatedAt,
            _ => throw new InvalidOperationException("Unsupported type")
        };
    }

    public static void AddPlayerUploadData(IEnumerable<PostPlayerRequest> requests)
    {
        foreach (var request in requests)
        {
            UpdateCacheIfNeeded(request.LocalContentId, request, _UploadPlayers, _UploadedPlayersCache);
        }
    }


    public static CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
    public static async Task PostPlayerData(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                while (!_UploadPlayers.IsEmpty)
                {
                    await ProcessPlayerUploadBatch(cancellationToken).ConfigureAwait(false);
                }

                await Task.Delay(TimeSpan.FromSeconds(20), cancellationToken).ConfigureAwait(false);
            }
        }
        catch (TaskCanceledException e) when (!e.CancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(e, "Timeout while posting player data");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("PostPlayerData was canceled.");
        }
        catch (HttpRequestException e)
        {
            _logger.LogWarning(e, "Network error while posting player data");
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Unexpected error while posting player data");
        }
    }
    public static void StopUploads()
    {
        if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
        {
            _cancellationTokenSource.Cancel();
            _logger.LogInformation("Upload tasks have been canceled.");
        }
    }
    private static async Task ProcessPlayerUploadBatch(CancellationToken cancellationToken,
    int batchSize = 200,
    int maxRetries = 3)
    {
        if (_UploadPlayers.IsEmpty) return;
        if (cancellationToken.IsCancellationRequested)
            return;

        var itemsToUpload = _UploadPlayers.Take(batchSize).Select(kvp => kvp.Value).ToList();
        //_logger.LogInformation($"Uploading {itemsToUpload.Count} Player items. TotalCount: {_UploadPlayers.Count}");

        int retryCount = 0;
        bool uploadSuccess = false;

        while (!_cancellationTokenSource.IsCancellationRequested && !_UploadPlayers.IsEmpty && !uploadSuccess && retryCount < maxRetries)
        {
            var uploadResult = await ApiClient.Instance.PostPlayersWithDetails(itemsToUpload).ConfigureAwait(false);
            
            if (uploadResult.Success)
            {
                foreach (var item in itemsToUpload)
                {
                    var key = GetKey(item);
                    _UploadPlayers.TryRemove(key, out _);
                    _UploadedPlayersCache[key] = item;
                }
                //_logger.LogInformation("Player upload successful, items added to cache.");
                uploadSuccess = true;
            }
            else if (uploadResult.AuthenticationFailure)
            {
                _logger.LogWarning("Authentication failure detected during player upload. Please check your API key configuration.");
                
                // Show user notification about authentication failure
                Plugin.Notification.AddNotification(new Dalamud.Interface.ImGuiNotification.Notification
                {
                    Content = "AlphaScope: Authentication failed. Please check your API key configuration.",
                    Type = Dalamud.Interface.ImGuiNotification.NotificationType.Error,
                    Minimized = false
                });
                
                // Mark as not logged in so user knows there's an issue
                Plugin.Instance.Configuration.LoggedIn = false;
                Plugin.Instance.Configuration.Save();
                
                // Stop retrying for this batch since authentication is invalid
                break;
            }
            else
            {
                retryCount++;
                _logger.LogWarning($"Player upload attempt {retryCount} failed. Retrying...");

                try
                {
                    await Task.Delay(1500 * retryCount, cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    _logger.LogInformation("Upload process canceled during delay.");
                    return;
                }
            }
        }

        if (!uploadSuccess)
        {
            _logger.LogError("Player upload failed after multiple attempts, items could not be uploaded.");
        }

        //_logger.LogInformation("ProcessPlayerUploadBatch completed.");

        if (!_UploadPlayers.IsEmpty)
        {
            //_logger.LogInformation("Waiting before next Player upload attempt as there are still items in the list.");
            try
            {
                await Task.Delay(1000, cancellationToken);
            }
            catch (TaskCanceledException)
            {
                //_logger.LogInformation("Upload process canceled during final delay.");
                return;
            }
        }
    }


    private static ulong GetKey<T>(T request)
    {
        return request switch
        {
            PostPlayerRequest player => player.LocalContentId,
            _ => throw new InvalidOperationException("Unsupported type")
        };
    }

    public static uint? GetCurrentWorld()
    {
        uint currentWorld = _clientState.LocalPlayer?.CurrentWorld.RowId ?? 0;
        if (currentWorld == 0)
            return null;
        return currentWorld;
    }



    public IReadOnlyList<string> GetAllAccountNamesForCharacter(ulong playerContentId)
    {
        using var scope = _serviceProvider.CreateScope();
        using var dbContext = scope.ServiceProvider.GetRequiredService<RetainerTrackContext>();
        return dbContext.Players.Where(p => playerContentId == p.LocalContentId)
            .SelectMany(player =>
                dbContext.Players.Where(x => x.AccountId == player.AccountId && player.AccountId != null))
            .Select(x => x.Name)
            .Where(x => !string.IsNullOrEmpty(x))
            .Cast<string>()
            .ToList()
            .AsReadOnly();
    }


    private void HandleContentIdMappingFallback(PlayerMapping mapping)
    {
        try
        {
            if (mapping.ContentId == 0 || string.IsNullOrEmpty(mapping.PlayerName))
                return;

            if (_playerCache.TryGetValue(mapping.ContentId, out CachedPlayer? cachedPlayer))
            {
                if (mapping.PlayerName == cachedPlayer.Name && mapping.AccountId == cachedPlayer.AccountId)
                    return;
            }

            using (var scope = _serviceProvider.CreateScope())
            {
                using var dbContext = scope.ServiceProvider.GetRequiredService<RetainerTrackContext>();
                var dbPlayer = dbContext.Players.Find(mapping.ContentId);
                if (dbPlayer == null)
                    dbContext.Players.Add(new Player
                    {
                        LocalContentId = mapping.ContentId,
                        Name = mapping.PlayerName,
                        AccountId = mapping.AccountId,
                    });
                else
                {
                    dbPlayer.Name = mapping.PlayerName;
                    dbPlayer.AccountId ??= mapping.AccountId;
                    dbContext.Entry(dbPlayer).State = EntityState.Modified;
                }

                int changeCount = dbContext.SaveChanges();
                if (changeCount > 0)
                {
                    //_logger.LogDebug("Saved fallback player mappings for {ContentId} / {Name} / {AccountId}", mapping.ContentId, mapping.PlayerName, mapping.AccountId);
                }

                var newCachedPlayer = new CachedPlayer
                {
                    AccountId = mapping.AccountId,
                    Name = mapping.PlayerName,
                    AvatarLink = null,
                };
                _playerCache[mapping.ContentId] = newCachedPlayer;
                
                _recentlyScannedPlayers[mapping.ContentId] = (newCachedPlayer, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            }
        }
        catch (DbUpdateException e)
        {
            _logger.LogError(e, "Database error while persisting singular mapping for {ContentId} / {Name} / {AccountId}",
                mapping.ContentId, mapping.PlayerName, mapping.AccountId);
        }
        catch (InvalidOperationException e)
        {
            _logger.LogWarning(e, "Invalid operation while persisting singular mapping for {ContentId} / {Name} / {AccountId}",
                mapping.ContentId, mapping.PlayerName, mapping.AccountId);
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Unexpected error while persisting singular mapping for {ContentId} / {Name} / {AccountId}",
                mapping.ContentId, mapping.PlayerName, mapping.AccountId);
        }
    }

    public static SemaphoreSlim processPlayers = new SemaphoreSlim(1, 999);
    public async Task HandleContentIdMappingAsync(IReadOnlyList<PlayerMapping> mappings)
    {
        // Filter to only players that actually need updates (not cached or have changes)
        var updates = new List<PlayerMapping>();
        var validMappings = mappings.DistinctBy(x => x.ContentId)
            .Where(mapping => mapping.ContentId != 0 && !string.IsNullOrEmpty(mapping.PlayerName))
            .ToList();

        foreach (var mapping in validMappings)
        {
            // Check if player is in recently scanned cache (to avoid immediate re-processing)
            if (_recentlyScannedPlayers.TryGetValue(mapping.ContentId, out var recentData))
            {
                var timeSinceLastScan = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - recentData.ScannedAt;
                if (timeSinceLastScan < 300) // Skip if scanned within last 5 minutes
                {
                    continue;
                }
            }

            // Check if we need to update based on cache
            if (_playerCache.TryGetValue(mapping.ContentId, out var cachedPlayer))
            {
                // Only update if data has actually changed
                if (mapping.PlayerName == cachedPlayer.Name && mapping.AccountId == cachedPlayer.AccountId)
                {
                    continue; // No changes needed
                }
            }
            
            updates.Add(mapping);
        }
        
        _logger.LogInformation($"Processing {updates.Count} player updates out of {mappings.Count} total mappings");
        if (updates.Count == 0)
            return;

        await processPlayers.WaitAsync().ConfigureAwait(false);

        try
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                using var dbContext = scope.ServiceProvider.GetRequiredService<RetainerTrackContext>();
                foreach (var update in updates)
                {
                    var dbPlayer = dbContext.Players.Find(update.ContentId);
                    if (dbPlayer == null)
                        dbContext.Players.Add(new Player
                        {
                            LocalContentId = update.ContentId,
                            Name = update.PlayerName,
                            AccountId = update.AccountId,
                        });
                    else
                    {
                        dbPlayer.Name = update.PlayerName;
                        dbPlayer.AccountId ??= update.AccountId;
                        dbContext.Entry(dbPlayer).State = EntityState.Modified;
                    }
                }

                _logger.LogInformation($"Attempting to save {updates.Count} players to database...");
                int changeCount = await dbContext.SaveChangesAsync();
                if (changeCount > 0)
                {
                    // foreach (var update in updates)
                    //_logger.LogDebug("  {ContentId} = {Name} ({AccountId})", update.ContentId, update.PlayerName,  update.AccountId);

                    _logger.LogInformation($"Successfully saved {changeCount} player mappings to database");
                }
                else
                {
                    _logger.LogWarning("Database save completed but no rows were affected");
                }
            }

            foreach (var player in updates)
            {
                var cachedPlayer = new CachedPlayer
                {
                    AccountId = player.AccountId,
                    Name = player.PlayerName,
                    AvatarLink = null,
                };
                _playerCache[player.ContentId] = cachedPlayer;
                
                _recentlyScannedPlayers[player.ContentId] = (cachedPlayer, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            }
        }
        catch (DbUpdateException e)
        {
            _logger.LogError(e, "Database error while persisting multiple mappings, attempting non-batch update");
            foreach (var update in updates)
            {
                HandleContentIdMappingFallback(update);
            }
        }
        catch (InvalidOperationException e)
        {
            _logger.LogWarning(e, "Invalid operation while persisting multiple mappings, attempting non-batch update");
            foreach (var update in updates)
            {
                HandleContentIdMappingFallback(update);
            }
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Unexpected error while persisting multiple mappings, attempting non-batch update");
            foreach (var update in updates)
            {
                HandleContentIdMappingFallback(update);
            }
        }

        processPlayers.Release();
    }

    public class CachedPlayer
    {
        public required ulong? AccountId { get; init; }
        public required string Name { get; init; }
        public string? AvatarLink { get; set; }
    }

    /// <summary>
    /// Updates the avatar link for a cached player and persists it to the database
    /// </summary>
    public static void UpdateCachedPlayerAvatar(ulong contentId, string avatarLink)
    {
        _logger?.LogInformation($"[AVATAR DEBUG] UpdateCachedPlayerAvatar called - ContentId: {contentId}, AvatarLink: '{avatarLink}'");
        
        if (_playerCache.TryGetValue(contentId, out var cachedPlayer) && cachedPlayer != null)
        {
            cachedPlayer.AvatarLink = avatarLink;
            _logger?.LogInformation($"[AVATAR DEBUG] Updated avatar for cached player {cachedPlayer.Name}: {avatarLink}");
            
            // Persist to database
            try
            {
                using var scope = _serviceProvider.CreateScope();
                using var dbContext = scope.ServiceProvider.GetRequiredService<RetainerTrackContext>();
                var dbPlayer = dbContext.Players.Find(contentId);
                if (dbPlayer != null)
                {
                    dbPlayer.AvatarLink = avatarLink;
                    dbContext.SaveChanges();
                    _logger?.LogInformation($"[AVATAR DEBUG] Persisted avatar URL to database for player {cachedPlayer.Name}");
                }
                else
                {
                    _logger?.LogWarning($"[AVATAR DEBUG] Could not find player in database to update avatar - ContentId: {contentId}");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"[AVATAR DEBUG] Failed to persist avatar URL for player {cachedPlayer.Name}");
            }
        }
        else
        {
            _logger?.LogWarning($"[AVATAR DEBUG] Could not find player in cache to update avatar - ContentId: {contentId}");
        }
    }

    /// <summary>
    /// Checks if a player is already cached locally
    /// </summary>
    public static bool IsPlayerCached(ulong contentId)
    {
        return _playerCache.ContainsKey(contentId);
    }

    /// <summary>
    /// Queues a player for background Lodestone data refresh
    /// </summary>
    /// <param name="contentId">Content ID of the player to refresh</param>
    /// <param name="isNewPlayer">If true, adds to priority queue for immediate processing</param>
    public static void QueuePlayerForLodestoneRefresh(ulong contentId, bool isNewPlayer = false)
    {
        try
        {
            var refreshService = _serviceProvider.GetService<LodestoneRefreshService>();
            refreshService?.QueuePlayerForRefresh(contentId, isNewPlayer);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, $"Failed to queue player {contentId} for Lodestone refresh");
        }
    }

    /// <summary>
    /// Forces an immediate Lodestone refresh for a specific player
    /// </summary>
    public static async Task<bool> RefreshPlayerImmediately(ulong contentId)
    {
        try
        {
            var refreshService = _serviceProvider.GetService<LodestoneRefreshService>();
            if (refreshService != null)
            {
                return await refreshService.RefreshPlayerImmediately(contentId);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, $"Failed to perform immediate refresh for player {contentId}");
        }
        return false;
    }

}