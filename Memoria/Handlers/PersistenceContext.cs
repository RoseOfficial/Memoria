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
using Dalamud.Bindings.ImGui;
using Lumina.Excel.Sheets;

// Microsoft framework dependencies
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

// Memoria internal dependencies
using Memoria.API;
using Memoria.API.Models.Requests.Player;
using Memoria.GUI;
using Memoria.Persistence;
using Memoria.Services;

// Static imports for specific functionality
using static FFXIVClientStructs.Havok.Animation.Deform.Skinning.hkaMeshBinding;
using static Memoria.Handlers.PersistenceContext;

namespace Memoria.Handlers;

/// <summary>
/// Core data persistence and synchronization handler for Memoria.
/// Manages local data caching, background data uploads to server, and coordination between
/// local database storage and server API synchronization. Handles player data
/// lifecycle from initial discovery through upload and caching.
/// </summary>
internal sealed class PersistenceContext
{
    /// <summary>
    /// Logger for persistence operations and debugging
    /// </summary>
    public static ILogger<PersistenceContext> _logger = null!;
    
    /// <summary>
    /// Dalamud client state service for accessing current player information
    /// </summary>
    public static IClientState _clientState = null!;

    /// <summary>
    /// Dalamud player state service for reading local player ContentId and world without deprecated IClientState members
    /// </summary>
    public static IPlayerState _playerState = null!;

    /// <summary>
    /// Service provider for dependency injection and database access
    /// </summary>
    public static IServiceProvider _serviceProvider = null!;
    
    
    /// <summary>
    /// Cache of player data indexed by Content ID for fast access
    /// </summary>
    public static readonly ConcurrentDictionary<ulong, CachedPlayer> _playerCache = new();
    
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

    /// <summary>UTC timestamp of the last successful batch upload. Null if none yet. Exposed for the Settings status panel.</summary>
    public static DateTime? LastSuccessfulUploadAt;

    /// <summary>
    /// Durable write-ahead log for pending uploads. Survives plugin crashes so scans are not
    /// lost between capture and successful POST to the server.
    /// </summary>
    public static UploadOutbox _outbox = null!;
    
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
    private static PersistenceContext? _instance = null;
    
    /// <summary>
    /// Gets the singleton instance of the persistence context
    /// </summary>
    public static PersistenceContext Instance
    {
        get
        {
            return _instance!;
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
    /// <param name="playerState">Dalamud player state service for ContentId and world reads</param>
    public PersistenceContext(ILogger<PersistenceContext> logger, IClientState clientState,
        IServiceProvider serviceProvider, IDataManager data, IPlayerState playerState)
    {
        if (_instance == null)
        {
            _instance = this;
        }

        _logger = logger;
        _clientState = clientState;
        _playerState = playerState;
        _serviceProvider = serviceProvider;

        // Force clear all static caches immediately on startup
        _playerCache.Clear();
        _UploadPlayers.Clear();
        _UploadedPlayersCache.Clear();
        _recentlyScannedPlayers.Clear();

        // Initialize durable upload outbox and replay anything left over from a previous session
        var configDir = Plugin.Instance._pluginInterface.GetPluginConfigDirectory();
        _outbox = new UploadOutbox(System.IO.Path.Combine(configDir, "pending_uploads.jsonl"));
        var replayed = 0;
        foreach (var pending in _outbox.ReadPending())
        {
            _UploadPlayers[pending.LocalContentId] = pending;
            replayed++;
        }
        if (replayed > 0)
            _logger.LogInformation("PersistenceContext: Replayed {Count} pending uploads from outbox", replayed);

        // Load existing data from database into memory caches
        ReloadCache();

        // Start background upload processing
        _cancellationTokenSource = new CancellationTokenSource();
        _ = Task.Run(() => PostPlayerData(_cancellationTokenSource.Token), _cancellationTokenSource.Token);
        _logger.LogInformation("PersistenceContext: Background server upload task started");

        // Start avatar backfill loop. Players freshly scanned after startup land in
        // the cache with AvatarLink=null because the scan payload doesn't carry it.
        // The server-side LodestoneEnrichmentService has the avatar (eventually); this
        // loop pulls it into the local cache so the in-game UI stops showing blank
        // avatar boxes. The legacy plugin-side LodestoneRefreshService still runs but
        // it goes through NetStone directly and is slow; this is the fast path.
        _ = Task.Run(() => BackfillAvatarsLoop(_cancellationTokenSource.Token), _cancellationTokenSource.Token);
        _logger.LogInformation("PersistenceContext: Background avatar backfill task started");
    }
    /// <summary>
    /// Clears all in-memory caches. Useful when database is reset or when fresh start is needed.
    /// </summary>
    public static void ClearCache()
    {
        _playerCache.Clear();
        _UploadPlayers.Clear();
        _UploadedPlayersCache.Clear();
        _recentlyScannedPlayers.Clear();
        _logger?.LogDebug("In-memory caches cleared");
    }

    /// <summary>
    /// Clears the in-memory cache and triggers a fresh hydration from the server. Server data
    /// is unaffected — this is a local-view reset, useful when the cache has drifted or the UI
    /// wants a forced refresh. Historically this cleared a local SQLite database; there is no
    /// longer one, so the method is kept under the same name for UI compatibility but the work
    /// it does is just cache reset + server fetch.
    /// </summary>
    public static void ClearDatabase()
    {
        _logger?.LogInformation("ClearDatabase: resetting in-memory cache and refetching from server");
        ClearCache();
        ReloadCache();
    }

    /// <summary>
    /// Rehydrates the in-memory player cache from the central server. Fire-and-forget; the UI
    /// stays empty until the first page returns, and if the server is unreachable the cache
    /// stays empty until a future invocation succeeds.
    /// </summary>
    public static void ReloadCache()
    {
        _ = Task.Run(ReloadCacheAsync);
    }

    private static async Task ReloadCacheAsync()
    {
        try
        {
            ClearCache();

            var apiClient = _serviceProvider.GetRequiredService<ApiClient>();
            var cursor = 0;
            var playerCount = 0;
            var avatarCount = 0;

            while (true)
            {
                var query = new API.Query.Player.PlayerQueryObject
                {
                    Cursor = cursor,
                    IsFetching = true,
                };

                var response = await apiClient.GetPlayersAsync<API.Models.Responses.Player.PlayerSearchDto>(query).ConfigureAwait(false);
                if (!response.IsSuccess || response.Value.Page is null)
                {
                    _logger?.LogWarning("Cache hydration from server halted: {Error}",
                        response.Error ?? response.Value.Message ?? "no data in response");
                    break;
                }

                var page = response.Value.Page;
                if (page.Data is null || page.Data.Count == 0)
                    break;

                foreach (var dto in page.Data)
                {
                    _playerCache[(ulong)dto.LocalContentId] = new CachedPlayer
                    {
                        AccountId = dto.AccountId.HasValue ? (ulong)dto.AccountId.Value : (ulong?)null,
                        Name = dto.Name ?? string.Empty,
                        AvatarLink = dto.AvatarLink,
                        HomeWorldId = dto.HomeWorldId.HasValue ? (ushort)dto.HomeWorldId.Value : (ushort?)null,
                        CurrentWorldId = dto.WorldId.HasValue ? (ushort)dto.WorldId.Value : (ushort?)null,
                        LastScannedAt = dto.LastScannedAt,
                        LodestoneJobData = dto.LodestoneJobData,
                        MainJobId = dto.MainJobId,
                        MainJobLevel = dto.MainJobLevel,
                        LastJobDataUpdate = dto.LastJobDataUpdate,
                        LodestoneMinionsData = dto.LodestoneMinionsData,
                        LastMinionsDataUpdate = dto.LastMinionsDataUpdate,
                        LodestoneMountsData = dto.LodestoneMountsData,
                        LastMountsDataUpdate = dto.LastMountsDataUpdate,
                    };
                    playerCount++;
                    if (!string.IsNullOrEmpty(dto.AvatarLink))
                        avatarCount++;
                }

                // End of pagination: the server reports how many rows remain past this page.
                if (page.NextCount <= 0)
                    break;

                // The controller paginates on LocalContentId >= Cursor and returns the last id
                // seen on this page; advance just past it for the next request.
                var nextCursor = page.LastCursor + 1;
                if (nextCursor == cursor)
                    break; // defensive: avoid infinite loop if cursor ever fails to advance
                cursor = nextCursor;
            }

            _logger?.LogInformation("Cache hydrated from server: {Count} players loaded, {AvatarCount} with avatars",
                playerCount, avatarCount);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to hydrate cache from server — UI will remain empty until a future sync succeeds");
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
                       playerRequest.CurrentWorldId != cachedPlayer.CurrentWorldId ||
                       // Phase 1 — these change frequently and should not be deduped away
                       playerRequest.OnlineStatusId != cachedPlayer.OnlineStatusId ||
                       playerRequest.TitleId != cachedPlayer.TitleId ||
                       playerRequest.GrandCompanyId != cachedPlayer.GrandCompanyId ||
                       playerRequest.FreeCompanyTag != cachedPlayer.FreeCompanyTag ||
                       playerRequest.CurrentMountId != cachedPlayer.CurrentMountId ||
                       playerRequest.CurrentMinionId != cachedPlayer.CurrentMinionId;

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
            // Only append to the outbox when the request actually landed in the upload queue.
            // UpdateCacheIfNeeded short-circuits on stationary players to avoid flooding the log
            // with identical snapshots; in that case the queued entry is not this request.
            if (_UploadPlayers.TryGetValue(request.LocalContentId, out var queued)
                && ReferenceEquals(queued, request))
            {
                _outbox?.Append(request);
            }
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
            _logger.LogDebug("PostPlayerData was canceled");
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
            _logger.LogDebug("Upload tasks have been canceled");
        }
    }

    /// <summary>
    /// Periodically pulls AvatarLink from the server for cached players that don't have one
    /// yet. Players freshly scanned after plugin start enter the cache with AvatarLink=null
    /// (the scan payload doesn't carry it); the server-side LodestoneEnrichmentService
    /// populates the avatar within seconds of the upload landing. Without this loop the
    /// plugin's UI would render blank avatar boxes until either ReloadCache is manually
    /// triggered or the legacy plugin-side LodestoneRefreshService gets around to fetching
    /// from NetStone (which is rate-limited and slow). 30s cadence + 20-player cap +
    /// 500ms inter-request delay = back-pressure that finishes a typical session-worth of
    /// new players in a few minutes without hammering the server.
    /// </summary>
    private static async Task BackfillAvatarsLoop(CancellationToken cancellationToken)
    {
        var initialDelay = TimeSpan.FromSeconds(15);
        var loopDelay = TimeSpan.FromSeconds(30);
        var perRequestDelay = TimeSpan.FromMilliseconds(500);
        const int MaxPerCycle = 20;

        try { await Task.Delay(initialDelay, cancellationToken).ConfigureAwait(false); }
        catch (OperationCanceledException) { return; }

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var snapshot = _playerCache
                    .Where(kvp => kvp.Value != null && string.IsNullOrEmpty(kvp.Value.AvatarLink))
                    .Select(kvp => kvp.Key)
                    .Take(MaxPerCycle)
                    .ToList();

                if (snapshot.Count > 0)
                {
                    var apiClient = _serviceProvider.GetRequiredService<ApiClient>();
                    foreach (var contentId in snapshot)
                    {
                        if (cancellationToken.IsCancellationRequested) return;
                        try
                        {
                            var result = await apiClient.GetPlayerByIdAsync((long)contentId).ConfigureAwait(false);
                            if (result.IsSuccess)
                            {
                                var avatar = result.Value.Player?.PlayerLodestone?.AvatarLink;
                                if (!string.IsNullOrEmpty(avatar))
                                    UpdateCachedPlayerAvatar(contentId, avatar);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogDebug(ex, "BackfillAvatarsLoop: server fetch failed for {ContentId}", contentId);
                        }
                        try { await Task.Delay(perRequestDelay, cancellationToken).ConfigureAwait(false); }
                        catch (OperationCanceledException) { return; }
                    }
                }
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "BackfillAvatarsLoop: cycle failed; will retry next iteration");
            }

            try { await Task.Delay(loopDelay, cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) { return; }
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
        
        // Enrich PostPlayerRequest objects with cached Lodestone data before upload
        var enrichedItemsToUpload = new List<PostPlayerRequest>();
        foreach (var item in itemsToUpload)
        {
            var enrichedItem = EnrichWithLodestoneCacheData(item);
            enrichedItemsToUpload.Add(enrichedItem);
        }
        
        _logger.LogInformation($"Uploading {enrichedItemsToUpload.Count} Player items. TotalCount: {_UploadPlayers.Count}");

        int retryCount = 0;
        bool uploadSuccess = false;

        while (!_cancellationTokenSource.IsCancellationRequested && !_UploadPlayers.IsEmpty && !uploadSuccess && retryCount < maxRetries)
        {
            var apiClient = _serviceProvider.GetRequiredService<ApiClient>();
            var uploadResult = await apiClient.PostPlayersWithDetails(enrichedItemsToUpload).ConfigureAwait(false);
            
            if (uploadResult.Success)
            {
                var uploadedKeys = new List<ulong>(itemsToUpload.Count);
                foreach (var item in itemsToUpload)
                {
                    var key = GetKey(item);
                    _UploadPlayers.TryRemove(key, out _);
                    _UploadedPlayersCache[key] = item;
                    uploadedKeys.Add(key);
                }
                // Drop successfully-uploaded entries from the durable outbox.
                _outbox?.Remove(uploadedKeys);
                LastSuccessfulUploadAt = DateTime.UtcNow;
                var config = Plugin.Instance?.Configuration;
                if (config is not null)
                {
                    config.TotalContributions += itemsToUpload.Count;
                    config.Save();
                }
                uploadSuccess = true;
            }
            else if (uploadResult.AuthenticationFailure)
            {
                _logger.LogWarning("Authentication failure detected during player upload. Please check your API key configuration.");
                
                // Show user notification about authentication failure
                Plugin.Notification.AddNotification(new Dalamud.Interface.ImGuiNotification.Notification
                {
                    Content = "Memoria: Authentication failed. Please check your API key configuration.",
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
        uint currentWorld = _playerState.CurrentWorld.RowId;
        if (currentWorld == 0)
            return null;
        return currentWorld;
    }

    /// <summary>
    /// Returns other characters known to share the given Account ID, excluding the specified character.
    /// Scans the in-memory player cache; no database access. The scan is O(n) over every cached player
    /// on every call, so per-frame callers should memoise if the cache routinely exceeds a few thousand
    /// entries.
    /// </summary>
    public static IReadOnlyList<(ulong ContentId, CachedPlayer Player)> GetAccountAltCharacters(
        ulong accountId,
        ulong excludeContentId)
    {
        return _playerCache
            .Where(kvp => kvp.Key != excludeContentId
                          && kvp.Value.AccountId.HasValue
                          && kvp.Value.AccountId.Value == accountId)
            .Select(kvp => (kvp.Key, kvp.Value))
            .ToList();
    }


    public static SemaphoreSlim processPlayers = new SemaphoreSlim(1, 999);

    /// <summary>
    /// Updates the in-memory caches for a batch of player mappings observed by the scan loop.
    /// The server receives these players via the scan upload pipeline (ObjectTableHandler enqueues
    /// a <see cref="PostPlayerRequest"/> per observed player every tick), so this method does not
    /// need to write anywhere durable itself.
    /// </summary>
    public async Task HandleContentIdMappingAsync(IReadOnlyList<PlayerMapping> mappings, ushort? currentWorldId = null)
    {
        var updates = new List<PlayerMapping>();
        var validMappings = mappings.DistinctBy(x => x.ContentId)
            .Where(mapping => mapping.ContentId != 0 && !string.IsNullOrEmpty(mapping.PlayerName))
            .ToList();

        foreach (var mapping in validMappings)
        {
            if (_recentlyScannedPlayers.TryGetValue(mapping.ContentId, out var recentData))
            {
                var timeSinceLastScan = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - recentData.ScannedAt;
                if (timeSinceLastScan < 300)
                    continue;
            }

            if (_playerCache.TryGetValue(mapping.ContentId, out var cachedPlayer))
            {
                // Skip when nothing the mapping carries differs from cache. Null fields
                // in the mapping mean "this capture path didn't observe it" — never treat
                // them as differences (and the update step below also preserves existing
                // values when the mapping is null). The earlier check only compared
                // Name and AccountId, which meant world-id corrections (DC travel,
                // server transfer) couldn't update the cache once seeded with a stale
                // value: the dedupe skipped, and the wrong HomeWorldId was rendered
                // forever. Comparing world ids closes that gap without re-introducing
                // the partial-payload clobber that motivated the original guard.
                var nameChanged = mapping.PlayerName != cachedPlayer.Name;
                var accountChanged = mapping.AccountId.HasValue && mapping.AccountId != cachedPlayer.AccountId;
                var homeWorldChanged = mapping.WorldId.HasValue && mapping.WorldId != cachedPlayer.HomeWorldId;
                var currentWorldChanged = mapping.CurrentWorldId.HasValue && mapping.CurrentWorldId != cachedPlayer.CurrentWorldId;
                if (!nameChanged && !accountChanged && !homeWorldChanged && !currentWorldChanged)
                    continue;
            }

            updates.Add(mapping);
        }

        if (updates.Count == 0)
            return;

        await processPlayers.WaitAsync().ConfigureAwait(false);
        try
        {
            foreach (var player in updates)
            {
                // Preserve existing Lodestone enrichment data if we already had this player cached —
                // the scan payload doesn't carry Lodestone fields and we don't want to clobber them.
                _playerCache.TryGetValue(player.ContentId, out var existing);

                var cachedPlayer = new CachedPlayer
                {
                    // Fall back to existing values when the mapping has nulls. CharacterNameResult
                    // sends a mapping with no AccountId / WorldIds — those nulls must not clobber
                    // values another capture path already established for this player.
                    AccountId = player.AccountId ?? existing?.AccountId,
                    Name = player.PlayerName,
                    AvatarLink = existing?.AvatarLink,
                    HomeWorldId = player.WorldId ?? existing?.HomeWorldId,
                    CurrentWorldId = player.CurrentWorldId ?? existing?.CurrentWorldId,
                    LastScannedAt = DateTime.UtcNow,
                    LodestoneJobData = existing?.LodestoneJobData,
                    MainJobId = existing?.MainJobId,
                    MainJobLevel = existing?.MainJobLevel,
                    LastJobDataUpdate = existing?.LastJobDataUpdate,
                    LodestoneMinionsData = existing?.LodestoneMinionsData,
                    LastMinionsDataUpdate = existing?.LastMinionsDataUpdate,
                    LodestoneMountsData = existing?.LodestoneMountsData,
                    LastMountsDataUpdate = existing?.LastMountsDataUpdate,
                };
                _playerCache[player.ContentId] = cachedPlayer;
                _recentlyScannedPlayers[player.ContentId] = (cachedPlayer, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            }
        }
        finally
        {
            processPlayers.Release();
        }
        await Task.CompletedTask;
    }

    public class CachedPlayer
    {
        public required ulong? AccountId { get; init; }
        public required string Name { get; init; }
        public string? AvatarLink { get; set; }
        public ushort? HomeWorldId { get; init; }
        public ushort? CurrentWorldId { get; init; }
        public DateTime? LastScannedAt { get; init; }
        public string? LodestoneJobData { get; init; }
        public byte? MainJobId { get; init; }
        public short? MainJobLevel { get; init; }
        public DateTime? LastJobDataUpdate { get; init; }
        public string? LodestoneMinionsData { get; init; }
        public DateTime? LastMinionsDataUpdate { get; init; }
        public string? LodestoneMountsData { get; init; }
        public DateTime? LastMountsDataUpdate { get; init; }
    }

    /// <summary>
    /// Updates the LastScannedAt timestamp for a cached player
    /// </summary>
    public static void UpdateCachedPlayerLastScannedAt(ulong contentId, DateTime lastScannedAt)
    {
        if (_playerCache.TryGetValue(contentId, out var cachedPlayer) && cachedPlayer != null)
        {
            // Create new cached player instance with updated timestamp (since LastScannedAt is init-only)
            _playerCache[contentId] = new CachedPlayer
            {
                AccountId = cachedPlayer.AccountId,
                Name = cachedPlayer.Name,
                AvatarLink = cachedPlayer.AvatarLink,
                HomeWorldId = cachedPlayer.HomeWorldId,
                CurrentWorldId = cachedPlayer.CurrentWorldId,
                LastScannedAt = lastScannedAt,
                LodestoneJobData = cachedPlayer.LodestoneJobData,
                MainJobId = cachedPlayer.MainJobId,
                MainJobLevel = cachedPlayer.MainJobLevel,
                LastJobDataUpdate = cachedPlayer.LastJobDataUpdate,
                LodestoneMinionsData = cachedPlayer.LodestoneMinionsData,
                LastMinionsDataUpdate = cachedPlayer.LastMinionsDataUpdate,
                LodestoneMountsData = cachedPlayer.LodestoneMountsData,
                LastMountsDataUpdate = cachedPlayer.LastMountsDataUpdate,
            };
            
        }
        else
        {
            _logger?.LogWarning($"Could not find player in cache to update LastScannedAt - ContentId: {contentId}");
        }
    }

    /// <summary>
    /// Updates the avatar link for a cached player. The next scan upload will carry it to the
    /// server via <see cref="EnrichWithLodestoneCacheData"/>.
    /// </summary>
    public static void UpdateCachedPlayerAvatar(ulong contentId, string avatarLink)
    {
        if (_playerCache.TryGetValue(contentId, out var cachedPlayer) && cachedPlayer != null)
        {
            cachedPlayer.AvatarLink = avatarLink;
        }
    }

    /// <summary>
    /// Updates the job data for a cached player. The next scan upload will carry it to the
    /// server via <see cref="EnrichWithLodestoneCacheData"/>.
    /// </summary>
    public static void UpdateCachedPlayerJobData(ulong contentId, string? lodestoneJobData, byte? mainJobId, short? mainJobLevel, DateTime? lastJobDataUpdate)
    {
        if (_playerCache.TryGetValue(contentId, out var cachedPlayer) && cachedPlayer != null)
        {
            _playerCache[contentId] = new CachedPlayer
            {
                AccountId = cachedPlayer.AccountId,
                Name = cachedPlayer.Name,
                AvatarLink = cachedPlayer.AvatarLink,
                HomeWorldId = cachedPlayer.HomeWorldId,
                CurrentWorldId = cachedPlayer.CurrentWorldId,
                LastScannedAt = cachedPlayer.LastScannedAt,
                LodestoneJobData = lodestoneJobData,
                MainJobId = mainJobId,
                MainJobLevel = mainJobLevel,
                LastJobDataUpdate = lastJobDataUpdate,
                LodestoneMinionsData = cachedPlayer.LodestoneMinionsData,
                LastMinionsDataUpdate = cachedPlayer.LastMinionsDataUpdate,
                LodestoneMountsData = cachedPlayer.LodestoneMountsData,
                LastMountsDataUpdate = cachedPlayer.LastMountsDataUpdate,
            };
        }
        else
        {
            _logger?.LogWarning($"Could not find player in cache to update job data - ContentId: {contentId}");
        }
    }

    /// <summary>
    /// Updates the minion data for a cached player. The next scan upload will carry it to the
    /// server via <see cref="EnrichWithLodestoneCacheData"/>.
    /// </summary>
    public static void UpdateCachedPlayerMinionsData(ulong contentId, string? lodestoneMinionsData, DateTime? lastMinionsDataUpdate)
    {
        if (_playerCache.TryGetValue(contentId, out var cachedPlayer) && cachedPlayer != null)
        {
            _playerCache[contentId] = new CachedPlayer
            {
                AccountId = cachedPlayer.AccountId,
                Name = cachedPlayer.Name,
                AvatarLink = cachedPlayer.AvatarLink,
                HomeWorldId = cachedPlayer.HomeWorldId,
                CurrentWorldId = cachedPlayer.CurrentWorldId,
                LastScannedAt = cachedPlayer.LastScannedAt,
                LodestoneJobData = cachedPlayer.LodestoneJobData,
                MainJobId = cachedPlayer.MainJobId,
                MainJobLevel = cachedPlayer.MainJobLevel,
                LastJobDataUpdate = cachedPlayer.LastJobDataUpdate,
                LodestoneMinionsData = lodestoneMinionsData,
                LastMinionsDataUpdate = lastMinionsDataUpdate,
                LodestoneMountsData = cachedPlayer.LodestoneMountsData,
                LastMountsDataUpdate = cachedPlayer.LastMountsDataUpdate,
            };
        }
        else
        {
            _logger?.LogWarning($"Could not find player in cache to update minion data - ContentId: {contentId}");
        }
    }

    /// <summary>
    /// Updates the mount data for a cached player. The next scan upload will carry it to the
    /// server via <see cref="EnrichWithLodestoneCacheData"/>.
    /// </summary>
    public static void UpdateCachedPlayerMountsData(ulong contentId, string? lodestoneMountsData, DateTime? lastMountsDataUpdate)
    {
        if (_playerCache.TryGetValue(contentId, out var cachedPlayer) && cachedPlayer != null)
        {
            _playerCache[contentId] = new CachedPlayer
            {
                AccountId = cachedPlayer.AccountId,
                Name = cachedPlayer.Name,
                AvatarLink = cachedPlayer.AvatarLink,
                HomeWorldId = cachedPlayer.HomeWorldId,
                CurrentWorldId = cachedPlayer.CurrentWorldId,
                LastScannedAt = cachedPlayer.LastScannedAt,
                LodestoneJobData = cachedPlayer.LodestoneJobData,
                MainJobId = cachedPlayer.MainJobId,
                MainJobLevel = cachedPlayer.MainJobLevel,
                LastJobDataUpdate = cachedPlayer.LastJobDataUpdate,
                LodestoneMinionsData = cachedPlayer.LodestoneMinionsData,
                LastMinionsDataUpdate = cachedPlayer.LastMinionsDataUpdate,
                LodestoneMountsData = lodestoneMountsData,
                LastMountsDataUpdate = lastMountsDataUpdate,
            };
        }
        else
        {
            _logger?.LogWarning($"Could not find player in cache to update mount data - ContentId: {contentId}");
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

    /// <summary>
    /// Enriches a PostPlayerRequest with cached Lodestone data (minions, mounts, job data) if available
    /// </summary>
    private static PostPlayerRequest EnrichWithLodestoneCacheData(PostPlayerRequest originalRequest)
    {
        if (_playerCache.TryGetValue(originalRequest.LocalContentId, out var cachedPlayer) && cachedPlayer != null)
        {
            // Create a new PostPlayerRequest with the original data plus cached Lodestone data
            return new PostPlayerRequest
            {
                LocalContentId = originalRequest.LocalContentId,
                Name = originalRequest.Name,
                AccountId = originalRequest.AccountId,
                HomeWorldId = originalRequest.HomeWorldId,
                CurrentWorldId = originalRequest.CurrentWorldId,
                TerritoryId = originalRequest.TerritoryId,
                // Forwarded explicitly because the field-by-field rebuild below dropped
                // it silently — TerritoryName was added to PostPlayerRequest after this
                // enricher was written, and "fields not listed get default(null)" meant
                // every enriched upload arrived at the server with TerritoryName=null,
                // so the TerritoryNames lookup table never populated despite the plugin
                // resolving "Limsa Lominsa Lower Decks" perfectly on the scan side.
                TerritoryName = originalRequest.TerritoryName,
                CurrentJobId = originalRequest.CurrentJobId,
                CurrentJobLevel = originalRequest.CurrentJobLevel,
                PlayerPos = originalRequest.PlayerPos,
                Customization = originalRequest.Customization,
                // Phase 1 — must be forwarded explicitly. The field-by-field rebuild
                // pattern silently drops anything not listed (see TerritoryName
                // post-mortem in CLAUDE.md), so adding a new PostPlayerRequest
                // field always requires also adding the line here.
                OnlineStatusId = originalRequest.OnlineStatusId,
                TitleId = originalRequest.TitleId,
                GrandCompanyId = originalRequest.GrandCompanyId,
                FreeCompanyTag = originalRequest.FreeCompanyTag,
                CurrentMountId = originalRequest.CurrentMountId,
                CurrentMinionId = originalRequest.CurrentMinionId,
                CreatedAt = originalRequest.CreatedAt,
                // Enrich with cached Lodestone data
                LodestoneJobData = cachedPlayer.LodestoneJobData,
                MainJobId = cachedPlayer.MainJobId,
                MainJobLevel = cachedPlayer.MainJobLevel,
                LastJobDataUpdate = cachedPlayer.LastJobDataUpdate,
                LodestoneMinionsData = cachedPlayer.LodestoneMinionsData,
                LastMinionsDataUpdate = cachedPlayer.LastMinionsDataUpdate,
                LodestoneMountsData = cachedPlayer.LodestoneMountsData,
                LastMountsDataUpdate = cachedPlayer.LastMountsDataUpdate
            };
        }
        
        // If no cached data is available, return the original request
        return originalRequest;
    }

}