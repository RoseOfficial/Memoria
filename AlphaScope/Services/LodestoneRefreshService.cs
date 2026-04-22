using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NetStone;
using NetStone.Search.Character;
using AlphaScope.Handlers;

namespace AlphaScope.Services;

/// <summary>
/// Represents a minion entry from Lodestone
/// </summary>
public class MinionInfo
{
    public int? MinionId { get; set; }
    public string? Name { get; set; }
    public string? IconUrl { get; set; }
    public DateTime? AcquiredDate { get; set; }
}

/// <summary>
/// Represents a mount entry from Lodestone
/// </summary>
public class MountInfo
{
    public int? MountId { get; set; }
    public string? Name { get; set; }
    public string? IconUrl { get; set; }
    public DateTime? AcquiredDate { get; set; }
}

/// <summary>
/// Background service that continuously refreshes character data from Lodestone.
/// Maintains a priority queue of players needing refresh and processes them with rate limiting.
/// Provides fresh avatar data, job information, and other Lodestone profile data.
/// </summary>
internal sealed class LodestoneRefreshService : IDisposable
{
    private readonly ILogger<LodestoneRefreshService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly Configuration _configuration;
    private readonly MinionDataService _minionDataService;
    private readonly MountDataService _mountDataService;
    private LodestoneClient? _lodestoneClient;
    
    /// <summary>
    /// Priority queue for high-priority players (new to database)
    /// </summary>
    private readonly ConcurrentQueue<ulong> _priorityQueue = new();
    
    /// <summary>
    /// Queue of Content IDs that need to be refreshed, processed in FIFO order
    /// </summary>
    private readonly ConcurrentQueue<ulong> _refreshQueue = new();
    
    /// <summary>
    /// Set of Content IDs currently queued to prevent duplicates
    /// </summary>
    private readonly ConcurrentDictionary<ulong, bool> _queuedPlayers = new();
    
    /// <summary>
    /// Cancellation token source for stopping the background processing
    /// </summary>
    private CancellationTokenSource _cancellationTokenSource = new();
    
    /// <summary>
    /// Background task that processes the refresh queue
    /// </summary>
    private Task? _processingTask;
    
    /// <summary>
    /// Gets configuration values for refresh service settings
    /// </summary>
    private Configuration Config => _configuration;

    public LodestoneRefreshService(ILogger<LodestoneRefreshService> logger, IServiceProvider serviceProvider, Configuration configuration, MinionDataService minionDataService, MountDataService mountDataService)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _minionDataService = minionDataService;
        _mountDataService = mountDataService;
        
        _logger.LogInformation("LodestoneRefreshService initialized - NetStone client will be created on first use");
        _logger.LogInformation("MinionDataService initialized with {MinionCount} minions mapped", _minionDataService.MinionCount);
        _logger.LogInformation("MountDataService initialized with {MountCount} mounts mapped", _mountDataService.MountCount);
    }
    
    /// <summary>
    /// Initializes the NetStone client lazily when first needed
    /// </summary>
    private async Task<LodestoneClient> GetLodestoneClientAsync()
    {
        if (_lodestoneClient == null)
        {
            _lodestoneClient = await LodestoneClient.GetClientAsync();
            _logger.LogInformation("NetStone client initialized successfully");
        }
        return _lodestoneClient;
    }

    /// <summary>
    /// Starts the background refresh service and populates the initial queue
    /// </summary>
    public async Task StartAsync()
    {
        
        if (!Config.LodestoneRefreshEnabled)
        {
            _logger.LogWarning("LodestoneRefreshService is disabled in configuration - LodestoneRefreshEnabled = {Enabled}", Config.LodestoneRefreshEnabled);
            return;
        }

        if (_processingTask != null)
        {
            _logger.LogWarning("LodestoneRefreshService is already running");
            return;
        }

        _logger.LogInformation($"Configuration - Enabled: {Config.LodestoneRefreshEnabled}, " +
                             $"Processing: 1 player per second, " +
                             $"Stale Threshold: {Config.LodestoneStaleThresholdHours}h, " +
                             $"Idle Delay: {Config.LodestoneRefreshIdleDelaySeconds}s");
        
        try
        {
            // Populate initial queue with existing players
            await PopulateInitialQueue();
            
            // Start background processing
            _processingTask = Task.Run(() => ProcessRefreshQueue(_cancellationTokenSource.Token));
            
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during LodestoneRefreshService startup");
            throw;
        }
    }

    /// <summary>
    /// Stops the background refresh service
    /// </summary>
    public async Task StopAsync()
    {
        if (_processingTask == null)
        {
            return;
        }

        _logger.LogInformation("Stopping LodestoneRefreshService...");
        
        _cancellationTokenSource.Cancel();
        
        try
        {
            await _processingTask;
        }
        catch (OperationCanceledException)
        {
            // Expected when cancelling
        }
        
        _processingTask = null;
        _logger.LogInformation("LodestoneRefreshService stopped");
    }

    /// <summary>
    /// Adds a player to the refresh queue (for newly discovered players)
    /// </summary>
    /// <param name="contentId">Content ID of the player to refresh</param>
    /// <param name="isNewPlayer">If true, adds to priority queue for immediate processing</param>
    public void QueuePlayerForRefresh(ulong contentId, bool isNewPlayer = false)
    {
        if (_queuedPlayers.TryAdd(contentId, true))
        {
            if (isNewPlayer)
            {
                _priorityQueue.Enqueue(contentId);
            }
            else
            {
                _refreshQueue.Enqueue(contentId);
            }
        }
        // Remove the excessive "already queued" logging - this gets called every frame
    }

    /// <summary>
    /// Force processes the next player in queue immediately (for testing)
    /// </summary>
    public async Task<bool> ForceProcessNextPlayer()
    {
        
        // Check priority queue first
        if (_priorityQueue.TryDequeue(out var contentId) || _refreshQueue.TryDequeue(out contentId))
        {
            _queuedPlayers.TryRemove(contentId, out _);
            await ProcessSinglePlayer(contentId);
            return true;
        }
        
        _logger.LogWarning("ForceProcessNextPlayer: No players in queue to process");
        return false;
    }

    /// <summary>
    /// Forces an immediate refresh of a specific player (bypasses queue)
    /// </summary>
    /// <param name="contentId">Content ID of the player to refresh immediately</param>
    public async Task<bool> RefreshPlayerImmediately(ulong contentId)
    {
        _logger.LogInformation($"Performing immediate refresh for player {contentId}");

        try
        {
            if (!PersistenceContext._playerCache.TryGetValue(contentId, out var cached) || cached is null)
            {
                _logger.LogWarning($"Player {contentId} not in cache for immediate refresh");
                return false;
            }

            return await RefreshPlayerData(contentId, cached);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error during immediate refresh for player {contentId}");
            return false;
        }
    }

    /// <summary>
    /// Populates the initial refresh queue from the in-memory player cache, prioritizing stale data.
    /// </summary>
    private Task PopulateInitialQueue()
    {
        try
        {
            var staleThreshold = DateTime.UtcNow.AddHours(-Config.LodestoneStaleThresholdHours);
            _logger.LogInformation($"PopulateInitialQueue: Stale threshold is {staleThreshold} (older than {Config.LodestoneStaleThresholdHours}h)");

            var totalPlayers = PersistenceContext._playerCache.Count;
            _logger.LogInformation($"PopulateInitialQueue: Total players in cache: {totalPlayers}");

            var playersToRefresh = PersistenceContext._playerCache
                .Where(kvp => kvp.Value.LastScannedAt == null || kvp.Value.LastScannedAt < staleThreshold)
                .OrderBy(kvp => kvp.Value.LastScannedAt ?? DateTime.MinValue)
                .Select(kvp => kvp.Key)
                .ToList();

            _logger.LogInformation($"PopulateInitialQueue: Found {playersToRefresh.Count} players needing refresh");

            var queuedCount = 0;
            foreach (var contentId in playersToRefresh)
            {
                if (_queuedPlayers.TryAdd(contentId, true))
                {
                    _refreshQueue.Enqueue(contentId);
                    queuedCount++;
                }
            }

            _logger.LogInformation($"PopulateInitialQueue: Successfully populated refresh queue with {queuedCount} players needing updates");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error populating initial refresh queue");
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Main background processing loop that continuously processes the refresh queue
    /// </summary>
    private async Task ProcessRefreshQueue(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Background refresh processing started");
        
        var loopCount = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                loopCount++;
                var priorityQueueSize = _priorityQueue.Count;
                var queueSize = _refreshQueue.Count;
                var queuedPlayersCount = _queuedPlayers.Count;
                
                // Only log every 30th loop to reduce spam (since we're processing every second now)
                if (loopCount % 30 == 1)
                {
                    _logger.LogInformation($"Processing loop #{loopCount} - Priority queue: {priorityQueueSize}, Regular queue: {queueSize}, Queued players: {queuedPlayersCount}");
                }
                
                // Process one player per second - priority queue first
                ulong contentId = 0;
                bool hasPlayer = false;
                
                if (_priorityQueue.TryDequeue(out contentId))
                {
                    hasPlayer = true;
                }
                else if (_refreshQueue.TryDequeue(out contentId))
                {
                    hasPlayer = true;
                }
                
                if (hasPlayer)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;
                    
                    // Remove from queued set
                    _queuedPlayers.TryRemove(contentId, out _);
                    
                    // Process the player
                    await ProcessSinglePlayer(contentId);
                    
                    // Wait 1 second before next player (as requested)
                    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                }
                else
                {
                    // No players to process, wait idle delay
                    var idleDelay = TimeSpan.FromSeconds(Config.LodestoneRefreshIdleDelaySeconds);
                    
                    // Only log wait message every 30th loop to reduce spam
                    if (loopCount % 30 == 1)
                    {
                        _logger.LogInformation($"No players in queue, waiting {idleDelay.TotalSeconds}s...");
                    }
                    await Task.Delay(idleDelay, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Processing loop cancelled");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in refresh queue processing loop");
                await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken); // Wait before retrying
            }
        }
        
        _logger.LogInformation("Background refresh processing stopped");
    }

    /// <summary>
    /// Processes a single player's Lodestone data refresh. Pulls the latest cached state from
    /// PersistenceContext, fetches Lodestone data, and writes updates back into the cache —
    /// which then rides the next scan upload to the server.
    /// </summary>
    private async Task ProcessSinglePlayer(ulong contentId)
    {
        try
        {
            if (!PersistenceContext._playerCache.TryGetValue(contentId, out var cached) || cached is null)
            {
                _logger.LogWarning($"ProcessSinglePlayer: Player {contentId} not in cache during refresh processing");
                return;
            }

            var success = await RefreshPlayerData(contentId, cached);
            if (!success)
                _logger.LogWarning($"ProcessSinglePlayer: Failed to refresh data for player {cached.Name}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"ProcessSinglePlayer: Error processing refresh for player {contentId}");
        }
    }

    /// <summary>
    /// Refreshes Lodestone data for a specific player by fetching directly from Lodestone.
    /// All updates land in <see cref="PersistenceContext._playerCache"/> via the Update*
    /// helpers; the next scan upload carries the enriched fields to the server.
    /// </summary>
    private async Task<bool> RefreshPlayerData(ulong contentId, PersistenceContext.CachedPlayer cached)
    {
        try
        {
            if (string.IsNullOrEmpty(cached.Name))
            {
                _logger.LogWarning($"Player {contentId} has no name, skipping Lodestone refresh");
                PersistenceContext.UpdateCachedPlayerLastScannedAt(contentId, DateTime.UtcNow);
                return true;
            }

            var (avatarUrl, jobLevels, mainJobId, mainJobLevel, minionsData, mountsData) =
                await FetchLodestonePlayerData(contentId, cached.Name, cached.HomeWorldId, cached.CurrentWorldId);

            var hasUpdates = false;

            if (!string.IsNullOrEmpty(avatarUrl) && avatarUrl != cached.AvatarLink)
            {
                PersistenceContext.UpdateCachedPlayerAvatar(contentId, avatarUrl);
                Plugin.AvatarCacheManager.ClearFailedDownloads(avatarUrl);
                hasUpdates = true;
            }
            else if (string.IsNullOrEmpty(avatarUrl))
            {
                _logger.LogWarning($"Could not fetch avatar URL for player {cached.Name}");
            }

            if (jobLevels != null && jobLevels.Count > 0)
            {
                var jobDataJson = JsonSerializer.Serialize(jobLevels);
                if (jobDataJson != cached.LodestoneJobData)
                {
                    PersistenceContext.UpdateCachedPlayerJobData(contentId, jobDataJson, mainJobId, mainJobLevel, DateTime.UtcNow);
                    hasUpdates = true;
                }
            }
            else
            {
                _logger.LogWarning($"Could not extract job data for player {cached.Name}");
            }

            if (minionsData != null && minionsData.Count > 0)
            {
                var minionsDataJson = JsonSerializer.Serialize(minionsData);
                if (minionsDataJson != cached.LodestoneMinionsData)
                {
                    PersistenceContext.UpdateCachedPlayerMinionsData(contentId, minionsDataJson, DateTime.UtcNow);
                    _logger.LogInformation($"Updated minion data for {cached.Name} with {minionsData.Count} minions");
                    hasUpdates = true;
                }
            }
            else
            {
                _logger.LogDebug($"Could not extract minion data for player {cached.Name}");
            }

            if (mountsData != null && mountsData.Count > 0)
            {
                var mountsDataJson = JsonSerializer.Serialize(mountsData);
                if (mountsDataJson != cached.LodestoneMountsData)
                {
                    PersistenceContext.UpdateCachedPlayerMountsData(contentId, mountsDataJson, DateTime.UtcNow);
                    _logger.LogInformation($"Updated mount data for {cached.Name} with {mountsData.Count} mounts");
                    hasUpdates = true;
                }
            }
            else
            {
                _logger.LogDebug($"Could not extract mount data for player {cached.Name}");
            }

            // Always update LastScannedAt to track when we attempted refresh
            PersistenceContext.UpdateCachedPlayerLastScannedAt(contentId, DateTime.UtcNow);
            hasUpdates = true;

            return hasUpdates;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error refreshing Lodestone data for player {cached.Name}");
            // Still stamp LastScannedAt to avoid infinite retry loops
            PersistenceContext.UpdateCachedPlayerLastScannedAt(contentId, DateTime.UtcNow);
            return true;
        }
    }

    /// <summary>
    /// Fetches comprehensive player data from Lodestone using NetStone library
    /// </summary>
    private async Task<(string? AvatarUrl, Dictionary<byte, short>? JobLevels, byte? MainJobId, short? MainJobLevel, List<MinionInfo>? MinionsData, List<MountInfo>? MountsData)> FetchLodestonePlayerData(ulong contentId, string name, ushort? homeWorldId, ushort? currentWorldId)
    {
        try
        {
            if (string.IsNullOrEmpty(name))
            {
                _logger.LogWarning($"Player {contentId} has no name, cannot search Lodestone");
                return (null, null, null, null, null, null);
            }

            // Get the NetStone client (lazy initialization)
            var client = await GetLodestoneClientAsync();

            // Try to get world name for more reliable search
            string? worldName = null;
            uint? worldIdToUse = null;

            // Try HomeWorldId first, fall back to CurrentWorldId
            if (homeWorldId.HasValue)
            {
                worldIdToUse = homeWorldId.Value;
                worldName = Utils.GetWorldName(worldIdToUse.Value);
                _logger.LogDebug($"Using HomeWorldId {worldIdToUse} ({worldName}) for character search: {name}");
            }
            else if (currentWorldId.HasValue)
            {
                worldIdToUse = currentWorldId.Value;
                worldName = Utils.GetWorldName(worldIdToUse.Value);
                _logger.LogDebug($"Using CurrentWorldId {worldIdToUse} ({worldName}) for character search: {name}");
            }
            else
            {
                _logger.LogWarning($"Player {name} has no world information, searching all worlds");
            }

            // Validate world name if we have one
            if (!string.IsNullOrEmpty(worldName) && worldName == "Unknown")
            {
                _logger.LogWarning($"World ID {worldIdToUse} resolved to 'Unknown', falling back to search all worlds for {name}");
                worldName = null;
            }

            // Build search query with world filter if available
            var query = new CharacterSearchQuery()
            {
                CharacterName = name
            };

            // Add world filter if we have a valid world name
            if (!string.IsNullOrEmpty(worldName))
            {
                query.World = worldName;
                _logger.LogDebug($"Searching for character {name} on world {worldName}");
            }
            else
            {
                _logger.LogDebug($"Searching for character {name} across all worlds");
            }

            var searchResult = await client.SearchCharacter(query);
            var character = searchResult?.Results?.FirstOrDefault();

            if (character == null)
            {
                if (!string.IsNullOrEmpty(worldName))
                {
                    // If world-specific search failed, try without world filter as fallback
                    _logger.LogWarning($"Character {name} not found on world {worldName}, trying search across all worlds");
                    query = new CharacterSearchQuery() { CharacterName = name };
                    searchResult = await client.SearchCharacter(query);
                    character = searchResult?.Results?.FirstOrDefault();
                }

                if (character == null)
                {
                    var worldInfo = !string.IsNullOrEmpty(worldName) ? $" (searched {worldName} and all worlds)" : " (searched all worlds)";
                    _logger.LogWarning($"Could not find character {name} in Lodestone search{worldInfo}");
                    return (null, null, null, null, null, null);
                }
                else
                {
                    _logger.LogInformation($"Found character {name} via fallback search across all worlds");
                }
            }
            else
            {
                var searchInfo = !string.IsNullOrEmpty(worldName) ? $" on world {worldName}" : " via all-worlds search";
                _logger.LogDebug($"Successfully found character {name}{searchInfo}");
            }
            
            // Get character profile
            var profile = await client.GetCharacter(character.Id!);
            if (profile == null)
            {
                _logger.LogWarning($"Could not fetch profile for character {name}");
                return (null, null, null, null, null, null);
            }
            
            // Extract avatar URL
            var avatarUrl = profile.Avatar?.ToString();
            
            // Extract job data using NetStone's GetClassJobInfo() method
            var jobLevels = new Dictionary<byte, short>();
            byte? mainJobId = null;
            short? mainJobLevel = null;
            
            try
            {
                var classJobInfo = await profile.GetClassJobInfo();
                if (classJobInfo != null)
                {
                    // Iterate through all jobs using ClassJobDict
                    foreach (var (staticJob, jobEntry) in classJobInfo.ClassJobDict)
                    {
                        if (jobEntry.IsUnlocked && jobEntry.Level > 0)
                        {
                            var jobId = (byte)(int)staticJob; // Convert enum to byte
                            var level = (short)jobEntry.Level;
                            
                            // Validate job data
                            if (IsValidJobId(jobId) && IsValidJobLevel(level))
                            {
                                jobLevels[jobId] = level;
                                
                                // Track highest level job as main job
                                if (!mainJobLevel.HasValue || level > mainJobLevel.Value)
                                {
                                    mainJobId = jobId;
                                    mainJobLevel = level;
                                }
                            }
                        }
                    }
                    
                    _logger.LogDebug($"Extracted {jobLevels.Count} job levels for {name}");
                }
                else
                {
                    _logger.LogWarning($"GetClassJobInfo returned null for {name}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Could not extract job data for {name}");
            }
            
            // Extract minion data using NetStone's GetMinions() method  
            List<MinionInfo>? minionsData = null;
            try
            {
                var minionsCollectable = await profile.GetMinions();
                if (minionsCollectable != null)
                {
                    // Use reflection to inspect the structure since API is unclear
                    var collectablesProperty = minionsCollectable.GetType().GetProperty("Collectables");
                    if (collectablesProperty != null)
                    {
                        var collectables = collectablesProperty.GetValue(minionsCollectable);
                        if (collectables is System.Collections.IEnumerable enumerable)
                        {
                            minionsData = new List<MinionInfo>();
                            
                            foreach (var minion in enumerable.Cast<object>())
                            {
                                var nameProperty = minion.GetType().GetProperty("Name");
                                var minionName = nameProperty?.GetValue(minion)?.ToString() ?? "Unknown";
                                
                                // Try to extract icon URL from NetStone
                                string? iconUrl = null;
                                var iconProperty = minion.GetType().GetProperty("Icon");
                                if (iconProperty != null)
                                {
                                    var iconValue = iconProperty.GetValue(minion);
                                    if (iconValue != null)
                                    {
                                        iconUrl = iconValue.ToString();
                                    }
                                }
                                
                                // If no icon URL from NetStone, try to get minion ID and construct XIVAPI URL
                                int? minionId = null;
                                var idProperty = minion.GetType().GetProperty("Id");
                                if (idProperty != null)
                                {
                                    var idValue = idProperty.GetValue(minion);
                                    if (idValue != null && int.TryParse(idValue.ToString(), out var id))
                                    {
                                        minionId = id;
                                    }
                                }
                                
                                // If we don't have a minion ID, try to look it up by name using comprehensive service
                                if (!minionId.HasValue && !string.IsNullOrEmpty(minionName))
                                {
                                    var mappedId = _minionDataService.GetMinionId(minionName);
                                    if (mappedId.HasValue)
                                    {
                                        minionId = (int)mappedId.Value;
                                    }
                                    else
                                    {
                                        _logger.LogWarning($"Minion '{minionName}' not found in MinionDataService - no icon available");
                                    }
                                }

                                // If we have an ID but no icon URL, use XIVAPI with actual icon ID from Lumina
                                if (string.IsNullOrEmpty(iconUrl) && minionId.HasValue)
                                {
                                    // Get the actual icon ID from MinionDataService
                                    var actualIconId = _minionDataService.GetMinionIconId((uint)minionId.Value);
                                    if (actualIconId.HasValue)
                                    {
                                        iconUrl = GetXivapiIconUrl(actualIconId.Value);
                                    }
                                    else
                                    {
                                        _logger.LogWarning($"No icon ID found in MinionDataService for minion '{minionName}' (ID: {minionId})");
                                    }
                                }

                                // Log when we can't get an icon for debugging the remaining 10%
                                if (string.IsNullOrEmpty(iconUrl))
                                {
                                    _logger.LogWarning($"No icon available for minion '{minionName}' - NetStone icon: {iconProperty?.GetValue(minion)}, MinionDataService ID: {(minionId.HasValue ? minionId.ToString() : "not found")}");
                                }

                                var minionInfo = new MinionInfo
                                {
                                    Name = minionName,
                                    IconUrl = iconUrl,
                                    MinionId = minionId,
                                    AcquiredDate = null // Check if available in minion properties
                                };
                                
                                minionsData.Add(minionInfo);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Could not fetch minion data for {name}");
            }
            
            // Extract mount data using NetStone's GetMounts() method  
            List<MountInfo>? mountsData = null;
            try
            {
                var mountsCollectable = await profile.GetMounts();
                if (mountsCollectable != null)
                {
                    // Use reflection to inspect the structure since API is unclear
                    var collectablesProperty = mountsCollectable.GetType().GetProperty("Collectables");
                    if (collectablesProperty != null)
                    {
                        var collectables = collectablesProperty.GetValue(mountsCollectable);
                        if (collectables is System.Collections.IEnumerable enumerable)
                        {
                            mountsData = new List<MountInfo>();
                            
                            foreach (var mount in enumerable.Cast<object>())
                            {
                                var nameProperty = mount.GetType().GetProperty("Name");
                                var mountName = nameProperty?.GetValue(mount)?.ToString() ?? "Unknown";
                                
                                // Try to extract icon URL from NetStone
                                string? iconUrl = null;
                                var iconProperty = mount.GetType().GetProperty("Icon");
                                if (iconProperty != null)
                                {
                                    var iconValue = iconProperty.GetValue(mount);
                                    if (iconValue != null)
                                    {
                                        iconUrl = iconValue.ToString();
                                    }
                                }
                                
                                // If no icon URL from NetStone, try to get mount ID and construct XIVAPI URL
                                int? mountId = null;
                                var idProperty = mount.GetType().GetProperty("Id");
                                if (idProperty != null)
                                {
                                    var idValue = idProperty.GetValue(mount);
                                    if (idValue != null && int.TryParse(idValue.ToString(), out var id))
                                    {
                                        mountId = id;
                                    }
                                }
                                
                                // If we don't have a mount ID, try to look it up by name using comprehensive service
                                if (!mountId.HasValue && !string.IsNullOrEmpty(mountName))
                                {
                                    var mappedId = _mountDataService.GetMountId(mountName);
                                    if (mappedId.HasValue)
                                    {
                                        mountId = (int)mappedId.Value;
                                    }
                                    else
                                    {
                                        _logger.LogWarning($"Mount '{mountName}' not found in MountDataService - no icon available");
                                    }
                                }

                                // If we have an ID but no icon URL, use XIVAPI with actual icon ID from Lumina
                                if (string.IsNullOrEmpty(iconUrl) && mountId.HasValue)
                                {
                                    // Get the actual icon ID from MountDataService
                                    var actualIconId = _mountDataService.GetMountIconId((uint)mountId.Value);
                                    if (actualIconId.HasValue)
                                    {
                                        iconUrl = GetXivapiIconUrl(actualIconId.Value);
                                    }
                                    else
                                    {
                                        _logger.LogWarning($"No icon ID found in MountDataService for mount '{mountName}' (ID: {mountId})");
                                    }
                                }

                                // Log when we can't get an icon for debugging the remaining cases
                                if (string.IsNullOrEmpty(iconUrl))
                                {
                                    _logger.LogWarning($"No icon available for mount '{mountName}' - NetStone icon: {iconProperty?.GetValue(mount)}, MountDataService ID: {(mountId.HasValue ? mountId.ToString() : "not found")}");
                                }

                                var mountInfo = new MountInfo
                                {
                                    Name = mountName,
                                    IconUrl = iconUrl,
                                    MountId = mountId,
                                    AcquiredDate = null // Check if available in mount properties
                                };
                                
                                mountsData.Add(mountInfo);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Could not fetch mount data for {name}");
            }
            
            _logger.LogDebug($"Successfully fetched Lodestone data for {name}: Avatar={!string.IsNullOrEmpty(avatarUrl)}, Jobs={jobLevels?.Count ?? 0}, Minions={minionsData?.Count ?? 0}, Mounts={mountsData?.Count ?? 0}");
            
            return (avatarUrl, jobLevels?.Count > 0 ? jobLevels : null, mainJobId, mainJobLevel, minionsData, mountsData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error fetching Lodestone data for {name} using NetStone");
            return (null, null, null, null, null, null);
        }
    }

    // FFXIV job validation constants - kept for NetStone validation
    private const short MAX_JOB_LEVEL = 100;
    private const short MIN_JOB_LEVEL = 1;
    private const byte MIN_JOB_ID = 1;
    private const byte MAX_JOB_ID = 100; // Expanded range to capture all possible job IDs
    
    /// <summary>
    /// Constructs an XIVAPI icon URL using the actual icon ID from Lumina data
    /// </summary>
    /// <param name="iconId">The actual icon ID from the Companion sheet</param>
    /// <returns>XIVAPI icon URL</returns>
    private static string GetXivapiIconUrl(uint iconId)
    {
        // Use the actual icon ID from Lumina data to construct the XIVAPI URL
        // Format: https://xivapi.com/i/{folder}/{iconId}.png
        // where folder is calculated as (iconId / 1000) * 1000
        var folder = (iconId / 1000) * 1000;
        return $"https://xivapi.com/i/{folder:D6}/{iconId:D6}.png";
    }
    

    /// <summary>
    /// Validates if a job ID is within valid FFXIV range
    /// </summary>
    private bool IsValidJobId(byte jobId)
    {
        return jobId >= MIN_JOB_ID && jobId <= MAX_JOB_ID;
    }

    /// <summary>
    /// Validates if a job level is within valid FFXIV range
    /// </summary>
    private bool IsValidJobLevel(short level)
    {
        return level >= MIN_JOB_LEVEL && level <= MAX_JOB_LEVEL;
    }

    /// <summary>
    /// Gets current queue status for monitoring
    /// </summary>
    public (int PriorityQueueSize, int QueueSize, int QueuedPlayers) GetQueueStatus()
    {
        return (_priorityQueue.Count, _refreshQueue.Count, _queuedPlayers.Count);
    }

    public void Dispose()
    {
        StopAsync().GetAwaiter().GetResult();
        _cancellationTokenSource?.Dispose();
        _lodestoneClient?.Dispose();
    }
}