using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NetStone;
using NetStone.Search.Character;
using AlphaScope.Database;
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
            using var scope = _serviceProvider.CreateScope();
            using var dbContext = scope.ServiceProvider.GetRequiredService<RetainerTrackContext>();
            
            var player = await dbContext.Players.FindAsync(contentId);
            if (player == null)
            {
                _logger.LogWarning($"Player {contentId} not found for immediate refresh");
                return false;
            }

            var success = await RefreshPlayerData(player, dbContext);
            if (success)
            {
                await dbContext.SaveChangesAsync();
            }
            
            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error during immediate refresh for player {contentId}");
            return false;
        }
    }

    /// <summary>
    /// Populates the initial refresh queue with existing players, prioritizing stale data
    /// </summary>
    private async Task PopulateInitialQueue()
    {
        try
        {
            
            using var scope = _serviceProvider.CreateScope();
            using var dbContext = scope.ServiceProvider.GetRequiredService<RetainerTrackContext>();
            
            var staleThreshold = DateTime.UtcNow.AddHours(-Config.LodestoneStaleThresholdHours);
            _logger.LogInformation($"PopulateInitialQueue: Stale threshold is {staleThreshold} (older than {Config.LodestoneStaleThresholdHours}h)");
            
            // First, count total players in database
            var totalPlayers = await dbContext.Players.CountAsync();
            _logger.LogInformation($"PopulateInitialQueue: Total players in database: {totalPlayers}");
            
            // Get players ordered by LastScannedAt (oldest first), with nulls first
            var playersToRefresh = await dbContext.Players
                .Where(p => p.LastScannedAt == null || p.LastScannedAt < staleThreshold)
                .OrderBy(p => p.LastScannedAt ?? DateTime.MinValue)
                .Select(p => p.LocalContentId)
                .ToListAsync();

            _logger.LogInformation($"PopulateInitialQueue: Found {playersToRefresh.Count} players needing refresh");

            var queuedCount = 0;
            foreach (var contentId in playersToRefresh)
            {
                if (_queuedPlayers.TryAdd(contentId, true))
                {
                    _refreshQueue.Enqueue(contentId);
                    queuedCount++;
                }
                else
                {
                }
            }
            
            _logger.LogInformation($"PopulateInitialQueue: Successfully populated refresh queue with {queuedCount} players needing updates");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error populating initial refresh queue");
        }
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
    /// Processes a single player's Lodestone data refresh
    /// </summary>
    private async Task ProcessSinglePlayer(ulong contentId)
    {
        try
        {
            
            using var scope = _serviceProvider.CreateScope();
            using var dbContext = scope.ServiceProvider.GetRequiredService<RetainerTrackContext>();
            
            var player = await dbContext.Players.FindAsync(contentId);
            if (player == null)
            {
                _logger.LogWarning($"ProcessSinglePlayer: Player {contentId} not found during refresh processing");
                return;
            }

            
            var success = await RefreshPlayerData(player, dbContext);
            if (success)
            {
                await dbContext.SaveChangesAsync();
            }
            else
            {
                _logger.LogWarning($"ProcessSinglePlayer: Failed to refresh data for player {player.Name}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"ProcessSinglePlayer: Error processing refresh for player {contentId}");
        }
    }

    /// <summary>
    /// Refreshes Lodestone data for a specific player by fetching directly from Lodestone
    /// </summary>
    private async Task<bool> RefreshPlayerData(Player player, RetainerTrackContext dbContext)
    {
        try
        {
            
            // Fetch avatar URL directly from Lodestone using character name and world
            if (string.IsNullOrEmpty(player.Name))
            {
                _logger.LogWarning($"Player {player.LocalContentId} has no name, skipping Lodestone refresh");
                player.LastScannedAt = DateTime.UtcNow;
                return true;
            }
            
            var (avatarUrl, jobLevels, mainJobId, mainJobLevel, minionsData, mountsData) = await FetchLodestonePlayerData(player);
            
            var hasUpdates = false;
            
            // Handle avatar updates
            if (!string.IsNullOrEmpty(avatarUrl) && avatarUrl != player.AvatarLink)
            {
                player.AvatarLink = avatarUrl;
                hasUpdates = true;
                
                // Update cached player data so UI reflects changes immediately
                PersistenceContext.UpdateCachedPlayerAvatar(player.LocalContentId, player.AvatarLink);
                
                // Clear any failed download attempts for this new URL
                Plugin.AvatarCacheManager.ClearFailedDownloads(player.AvatarLink);
                
            }
            else if (string.IsNullOrEmpty(avatarUrl))
            {
                _logger.LogWarning($"Could not fetch avatar URL for player {player.Name}");
            }
            else
            {
            }
            
            // Handle job data updates
            if (jobLevels != null && jobLevels.Count > 0)
            {
                var jobDataJson = JsonSerializer.Serialize(jobLevels);
                
                if (jobDataJson != player.LodestoneJobData)
                {
                    var jobDataUpdateTime = DateTime.UtcNow;
                    player.LodestoneJobData = jobDataJson;
                    player.MainJobId = mainJobId;
                    player.MainJobLevel = mainJobLevel;
                    player.LastJobDataUpdate = jobDataUpdateTime;
                    hasUpdates = true;
                    
                    // Update cached player data so UI reflects changes immediately
                    PersistenceContext.UpdateCachedPlayerJobData(player.LocalContentId, jobDataJson, mainJobId, mainJobLevel, jobDataUpdateTime);
                    
                }
                else
                {
                }
            }
            else
            {
                _logger.LogWarning($"Could not extract job data for player {player.Name}");
            }
            
            // Handle minion data updates
            if (minionsData != null && minionsData.Count > 0)
            {
                var minionsDataJson = JsonSerializer.Serialize(minionsData);
                _logger.LogInformation($"Serialized minion data for {player.Name}: {minionsDataJson.Length} chars, first minion: {minionsData.FirstOrDefault()?.Name}");
                
                if (minionsDataJson != player.LodestoneMinionsData)
                {
                    var minionsDataUpdateTime = DateTime.UtcNow;
                    player.LodestoneMinionsData = minionsDataJson;
                    player.LastMinionsDataUpdate = minionsDataUpdateTime;
                    hasUpdates = true;
                    _logger.LogInformation($"Updated minion data for {player.Name} in database with {minionsData.Count} minions");
                    
                    // Update cached player data so UI reflects changes immediately
                    PersistenceContext.UpdateCachedPlayerMinionsData(player.LocalContentId, minionsDataJson, minionsDataUpdateTime);
                    
                }
                else
                {
                }
            }
            else
            {
                _logger.LogDebug($"Could not extract minion data for player {player.Name}");
            }
            
            // Handle mount data updates
            if (mountsData != null && mountsData.Count > 0)
            {
                var mountsDataJson = JsonSerializer.Serialize(mountsData);
                _logger.LogInformation($"Serialized mount data for {player.Name}: {mountsDataJson.Length} chars, first mount: {mountsData.FirstOrDefault()?.Name}");
                
                if (mountsDataJson != player.LodestoneMountsData)
                {
                    var mountsDataUpdateTime = DateTime.UtcNow;
                    player.LodestoneMountsData = mountsDataJson;
                    player.LastMountsDataUpdate = mountsDataUpdateTime;
                    hasUpdates = true;
                    _logger.LogInformation($"Updated mount data for {player.Name} in database with {mountsData.Count} mounts");
                    
                    // Update cached player data so UI reflects changes immediately
                    PersistenceContext.UpdateCachedPlayerMountsData(player.LocalContentId, mountsDataJson, mountsDataUpdateTime);
                    
                }
                else
                {
                }
            }
            else
            {
                _logger.LogDebug($"Could not extract mount data for player {player.Name}");
            }
            
            // Always update LastScannedAt to track when we attempted refresh
            var now = DateTime.UtcNow;
            player.LastScannedAt = now;
            hasUpdates = true;
            
            // Update cached player LastScannedAt so UI reflects changes immediately
            PersistenceContext.UpdateCachedPlayerLastScannedAt(player.LocalContentId, now);
            
            return hasUpdates;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error refreshing Lodestone data for player {player.Name}");
            
            // Still update LastScannedAt to avoid infinite retry loops
            var now = DateTime.UtcNow;
            player.LastScannedAt = now;
            
            // Update cached player LastScannedAt so UI reflects changes immediately
            PersistenceContext.UpdateCachedPlayerLastScannedAt(player.LocalContentId, now);
            
            return true; // Return true to save the timestamp update
        }
    }

    /// <summary>
    /// Fetches comprehensive player data from Lodestone using NetStone library
    /// </summary>
    private async Task<(string? AvatarUrl, Dictionary<byte, short>? JobLevels, byte? MainJobId, short? MainJobLevel, List<MinionInfo>? MinionsData, List<MountInfo>? MountsData)> FetchLodestonePlayerData(Player player)
    {
        try
        {
            if (string.IsNullOrEmpty(player.Name))
            {
                _logger.LogWarning($"Player {player.LocalContentId} has no name, cannot search Lodestone");
                return (null, null, null, null, null, null);
            }

            // Get the NetStone client (lazy initialization)
            var client = await GetLodestoneClientAsync();
            
            // Try to get world name for more reliable search
            string? worldName = null;
            uint? worldIdToUse = null;
            
            // Try HomeWorldId first, fall back to CurrentWorldId
            if (player.HomeWorldId.HasValue)
            {
                worldIdToUse = player.HomeWorldId.Value;
                worldName = Utils.GetWorldName(worldIdToUse.Value);
                _logger.LogDebug($"Using HomeWorldId {worldIdToUse} ({worldName}) for character search: {player.Name}");
            }
            else if (player.CurrentWorldId.HasValue)
            {
                worldIdToUse = player.CurrentWorldId.Value;
                worldName = Utils.GetWorldName(worldIdToUse.Value);
                _logger.LogDebug($"Using CurrentWorldId {worldIdToUse} ({worldName}) for character search: {player.Name}");
            }
            else
            {
                _logger.LogWarning($"Player {player.Name} has no world information, searching all worlds");
            }

            // Validate world name if we have one
            if (!string.IsNullOrEmpty(worldName) && worldName == "Unknown")
            {
                _logger.LogWarning($"World ID {worldIdToUse} resolved to 'Unknown', falling back to search all worlds for {player.Name}");
                worldName = null;
            }

            // Build search query with world filter if available
            var query = new CharacterSearchQuery()
            {
                CharacterName = player.Name
            };

            // Add world filter if we have a valid world name
            if (!string.IsNullOrEmpty(worldName))
            {
                query.World = worldName;
                _logger.LogDebug($"Searching for character {player.Name} on world {worldName}");
            }
            else
            {
                _logger.LogDebug($"Searching for character {player.Name} across all worlds");
            }

            var searchResult = await client.SearchCharacter(query);
            var character = searchResult?.Results?.FirstOrDefault();
            
            if (character == null)
            {
                if (!string.IsNullOrEmpty(worldName))
                {
                    // If world-specific search failed, try without world filter as fallback
                    _logger.LogWarning($"Character {player.Name} not found on world {worldName}, trying search across all worlds");
                    query = new CharacterSearchQuery() { CharacterName = player.Name };
                    searchResult = await client.SearchCharacter(query);
                    character = searchResult?.Results?.FirstOrDefault();
                }
                
                if (character == null)
                {
                    var worldInfo = !string.IsNullOrEmpty(worldName) ? $" (searched {worldName} and all worlds)" : " (searched all worlds)";
                    _logger.LogWarning($"Could not find character {player.Name} in Lodestone search{worldInfo}");
                    return (null, null, null, null, null, null);
                }
                else
                {
                    _logger.LogInformation($"Found character {player.Name} via fallback search across all worlds");
                }
            }
            else
            {
                var searchInfo = !string.IsNullOrEmpty(worldName) ? $" on world {worldName}" : " via all-worlds search";
                _logger.LogDebug($"Successfully found character {player.Name}{searchInfo}");
            }
            
            // Get character profile
            var profile = await client.GetCharacter(character.Id!);
            if (profile == null)
            {
                _logger.LogWarning($"Could not fetch profile for character {player.Name}");
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
                    
                    _logger.LogDebug($"Extracted {jobLevels.Count} job levels for {player.Name}");
                }
                else
                {
                    _logger.LogWarning($"GetClassJobInfo returned null for {player.Name}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Could not extract job data for {player.Name}");
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
                                var name = nameProperty?.GetValue(minion)?.ToString() ?? "Unknown";
                                
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
                                if (!minionId.HasValue && !string.IsNullOrEmpty(name))
                                {
                                    var mappedId = _minionDataService.GetMinionId(name);
                                    if (mappedId.HasValue)
                                    {
                                        minionId = (int)mappedId.Value;
                                    }
                                    else
                                    {
                                        _logger.LogWarning($"Minion '{name}' not found in MinionDataService - no icon available");
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
                                        _logger.LogWarning($"No icon ID found in MinionDataService for minion '{name}' (ID: {minionId})");
                                    }
                                }
                                
                                // Log when we can't get an icon for debugging the remaining 10%
                                if (string.IsNullOrEmpty(iconUrl))
                                {
                                    _logger.LogWarning($"No icon available for minion '{name}' - NetStone icon: {iconProperty?.GetValue(minion)}, MinionDataService ID: {(minionId.HasValue ? minionId.ToString() : "not found")}");
                                }
                                
                                var minionInfo = new MinionInfo
                                {
                                    Name = name,
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
                _logger.LogWarning(ex, $"Could not fetch minion data for {player.Name}");
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
                                var name = nameProperty?.GetValue(mount)?.ToString() ?? "Unknown";
                                
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
                                if (!mountId.HasValue && !string.IsNullOrEmpty(name))
                                {
                                    var mappedId = _mountDataService.GetMountId(name);
                                    if (mappedId.HasValue)
                                    {
                                        mountId = (int)mappedId.Value;
                                    }
                                    else
                                    {
                                        _logger.LogWarning($"Mount '{name}' not found in MountDataService - no icon available");
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
                                        _logger.LogWarning($"No icon ID found in MountDataService for mount '{name}' (ID: {mountId})");
                                    }
                                }
                                
                                // Log when we can't get an icon for debugging the remaining cases
                                if (string.IsNullOrEmpty(iconUrl))
                                {
                                    _logger.LogWarning($"No icon available for mount '{name}' - NetStone icon: {iconProperty?.GetValue(mount)}, MountDataService ID: {(mountId.HasValue ? mountId.ToString() : "not found")}");
                                }
                                
                                var mountInfo = new MountInfo
                                {
                                    Name = name,
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
                _logger.LogWarning(ex, $"Could not fetch mount data for {player.Name}");
            }
            
            _logger.LogDebug($"Successfully fetched Lodestone data for {player.Name}: Avatar={!string.IsNullOrEmpty(avatarUrl)}, Jobs={jobLevels?.Count ?? 0}, Minions={minionsData?.Count ?? 0}, Mounts={mountsData?.Count ?? 0}");
            
            return (avatarUrl, jobLevels?.Count > 0 ? jobLevels : null, mainJobId, mainJobLevel, minionsData, mountsData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error fetching Lodestone data for {player.Name} using NetStone");
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