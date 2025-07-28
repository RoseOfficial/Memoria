using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
/// Background service that continuously refreshes character data from Lodestone.
/// Maintains a priority queue of players needing refresh and processes them with rate limiting.
/// Provides fresh avatar data, job information, and other Lodestone profile data.
/// </summary>
internal sealed class LodestoneRefreshService : IDisposable
{
    private readonly ILogger<LodestoneRefreshService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly Configuration _configuration;
    
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

    public LodestoneRefreshService(ILogger<LodestoneRefreshService> logger, IServiceProvider serviceProvider, Configuration configuration)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        
        _logger.LogInformation("LodestoneRefreshService initialized");
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
            
            var (avatarUrl, jobLevels, mainJobId, mainJobLevel, minionsData) = await FetchLodestonePlayerData(player.Name, player.LocalContentId);
            
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
    /// Fetches comprehensive player data from Lodestone including avatar, job, and minion information
    /// </summary>
    private async Task<(string? AvatarUrl, Dictionary<byte, short>? JobLevels, byte? MainJobId, short? MainJobLevel, List<MinionInfo>? MinionsData)> FetchLodestonePlayerData(string characterName, ulong contentId)
    {
        try
        {
            using var httpClient = new System.Net.Http.HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "AlphaScope/1.0");
            
            // Search for character on Lodestone
            var searchUrl = $"https://na.finalfantasyxiv.com/lodestone/character/?q={Uri.EscapeDataString(characterName)}&worldname=";
            
            var searchResponse = await httpClient.GetStringAsync(searchUrl);
            
            // Parse search results to find the character's Lodestone ID
            var lodestoneId = ExtractLodestoneIdFromSearch(searchResponse, characterName);
            
            if (string.IsNullOrEmpty(lodestoneId))
            {
                _logger.LogWarning($"Could not find Lodestone ID for character {characterName}");
                return (null, null, null, null, null);
            }
            
            // Fetch character profile page
            var profileUrl = $"https://na.finalfantasyxiv.com/lodestone/character/{lodestoneId}/";
            
            var profileResponse = await httpClient.GetStringAsync(profileUrl);
            
            // Log a snippet of the HTML to check if we're getting valid content
            var htmlSnippet = profileResponse.Length > 500 ? profileResponse.Substring(0, 500) : profileResponse;
            
            // Extract avatar URL from profile page
            var avatarUrl = ExtractAvatarUrlFromProfile(profileResponse);
            
            // Fetch separate Class/Job page for job data
            var classJobUrl = $"https://na.finalfantasyxiv.com/lodestone/character/{lodestoneId}/class_job/";
            
            var classJobResponse = await httpClient.GetStringAsync(classJobUrl);
            
            // Extract job data from class/job page
            var (jobLevels, mainJobId, mainJobLevel) = ExtractJobDataFromProfile(classJobResponse);
            
            // Fetch minion page with error handling
            var minionUrl = $"https://na.finalfantasyxiv.com/lodestone/character/{lodestoneId}/minion/";
            string? minionResponse = null;
            
            try
            {
                minionResponse = await httpClient.GetStringAsync(minionUrl);
            }
            catch (HttpRequestException httpEx) when (httpEx.Message.Contains("404"))
            {
                _logger.LogDebug($"Minion page not found for {characterName} (404) - player may not have public minion data");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Failed to fetch minion page for {characterName}");
            }
            
            // Extract minion data from minion page (only if we got a response)
            var minionsData = !string.IsNullOrEmpty(minionResponse) ? ExtractMinionDataFromProfile(minionResponse) : null;
            
            if (!string.IsNullOrEmpty(avatarUrl))
            {
            }
            else
            {
                _logger.LogWarning($"Could not extract avatar URL from profile for {characterName}");
            }
            
            if (jobLevels != null && jobLevels.Count > 0)
            {
            }
            else
            {
                _logger.LogWarning($"Could not extract job data from profile for {characterName}");
            }
            
            if (minionsData != null && minionsData.Count > 0)
            {
            }
            else
            {
                _logger.LogDebug($"Could not extract minion data from profile for {characterName}");
            }
            
            return (avatarUrl, jobLevels, mainJobId, mainJobLevel, minionsData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error fetching Lodestone data for {characterName}");
            return (null, null, null, null, null);
        }
    }

    /// <summary>
    /// Extracts Lodestone character ID from search results HTML
    /// </summary>
    private string? ExtractLodestoneIdFromSearch(string html, string characterName)
    {
        try
        {
            // Look for character entry links in search results
            var pattern = @"<a\s+href=""/lodestone/character/(\d+)/""[^>]*>" + Regex.Escape(characterName);
            var match = Regex.Match(html, pattern, RegexOptions.IgnoreCase);
            
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
            
            // Fallback: look for any character link and extract the first one
            var fallbackPattern = @"/lodestone/character/(\d+)/";
            var fallbackMatch = Regex.Match(html, fallbackPattern);
            
            if (fallbackMatch.Success)
            {
                return fallbackMatch.Groups[1].Value;
            }
            
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error extracting Lodestone ID for {characterName}");
            return null;
        }
    }

    /// <summary>
    /// Extracts avatar URL from character profile HTML
    /// </summary>
    private string? ExtractAvatarUrlFromProfile(string html)
    {
        try
        {
            
            var allImgMatches = Regex.Matches(html, @"<img[^>]+src=""([^""]*)""\s*[^>]*>", RegexOptions.IgnoreCase);
            
            foreach (Match imgMatch in allImgMatches)
            {
                var imgUrl = imgMatch.Groups[1].Value;
                
                // Skip obvious non-avatar images
                if (imgUrl.Contains("banner/") || imgUrl.Contains("mogmog") || imgUrl.Contains("logo") || 
                    imgUrl.Contains("icon/") || imgUrl.Contains("ui/") || imgUrl.Contains("item/") ||
                    imgUrl.Contains("job/") || imgUrl.Contains("class/"))
                    continue;
                    
                // Look for character portrait patterns - these are the actual character avatars
                if ((imgUrl.Contains("img2.finalfantasyxiv.com/f/") && imgUrl.Contains("fc0.jpg")) ||
                    imgUrl.Contains("character") || imgUrl.Contains("avatar"))
                {
                    // Ensure URL is absolute
                    if (imgUrl.StartsWith("//"))
                        imgUrl = "https:" + imgUrl;
                    else if (imgUrl.StartsWith("/"))
                        imgUrl = "https://img.finalfantasyxiv.com" + imgUrl;
                    
                    return imgUrl;
                }
            }
            
            // If we didn't find a proper avatar, try more generic patterns
            var fallbackPatterns = new[]
            {
                @"character__detail__image[^>]*>.*?<img[^>]+src=""([^""]*\.jpg)""",
                @"frame__chara__face[^>]*>.*?<img[^>]+src=""([^""]*\.jpg)""",
                @"<div[^>]*class=""[^""]*character[^""]*""[^>]*>.*?<img[^>]+src=""([^""]*\.jpg)""",
                @"<img[^>]+src=""([^""]*\.jpg)""\s+alt=""[^""]*""[^>]*class=""[^""]*character[^""]*"""
            };
            
            foreach (var pattern in fallbackPatterns)
            {
                var match = Regex.Match(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
                if (match.Success)
                {
                    var url = match.Groups[1].Value;
                    
                    // Skip banner images and other non-avatar images
                    if (url.Contains("banner/") || url.Contains("mogmog") || url.Contains("logo") ||
                        url.Contains("icon/") || url.Contains("ui/") || url.Contains("item/"))
                        continue;
                        
                    // Ensure URL is absolute
                    if (url.StartsWith("//"))
                        url = "https:" + url;
                    else if (url.StartsWith("/"))
                        url = "https://img.finalfantasyxiv.com" + url;
                    
                    _logger.LogInformation($"Found fallback avatar URL: {url}");
                    return url;
                }
            }
            
            _logger.LogWarning("No valid character avatar found in profile HTML");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting avatar URL from profile");
            return null;
        }
    }

    // FFXIV job validation constants
    private const short MAX_JOB_LEVEL = 100;
    private const short MIN_JOB_LEVEL = 1;
    private const byte MIN_JOB_ID = 1;
    private const byte MAX_JOB_ID = 100; // Expanded range to capture all possible job IDs

    // Job name to ID mapping for text-based parsing
    private static readonly Dictionary<string, byte> JobNameToId = new Dictionary<string, byte>(StringComparer.OrdinalIgnoreCase)
    {
        // Tank Jobs
        {"Paladin", 19}, {"Warrior", 21}, {"Dark Knight", 32}, {"Gunbreaker", 37},
        // Healer Jobs  
        {"White Mage", 24}, {"Scholar", 28}, {"Astrologian", 33}, {"Sage", 40},
        // Melee DPS Jobs
        {"Monk", 20}, {"Dragoon", 22}, {"Ninja", 30}, {"Samurai", 34}, {"Reaper", 39}, {"Viper", 41},
        // Physical Ranged DPS
        {"Bard", 23}, {"Machinist", 31}, {"Dancer", 38},
        // Magical Ranged DPS
        {"Black Mage", 25}, {"Summoner", 27}, {"Red Mage", 35}, {"Blue Mage", 36}, {"Pictomancer", 42},
        // Base Classes
        {"Gladiator", 1}, {"Pugilist", 2}, {"Marauder", 3}, {"Lancer", 4}, {"Archer", 5},
        {"Conjurer", 6}, {"Thaumaturge", 7}, {"Arcanist", 26}, {"Rogue", 29},
        // Crafters
        {"Carpenter", 8}, {"Blacksmith", 9}, {"Armorer", 10}, {"Goldsmith", 11},
        {"Leatherworker", 12}, {"Weaver", 13}, {"Alchemist", 14}, {"Culinarian", 15},
        // Gatherers
        {"Miner", 16}, {"Botanist", 17}, {"Fisher", 18}
    };

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
    /// Extracts complete job level data from character profile HTML
    /// </summary>
    private (Dictionary<byte, short>? JobLevels, byte? MainJobId, short? MainJobLevel) ExtractJobDataFromProfile(string html)
    {
        try
        {
            
            
            var jobLevels = new Dictionary<byte, short>();
            byte? mainJobId = null;
            short? mainJobLevel = null;
            
            // Look for job data in the character profile
            // FFXIV Lodestone displays job levels in specific HTML patterns
            
            // Pattern 1: Look for job level data in class/job sections
            // Patterns removed - using text-based parsing only since ID patterns are unreliable
            var jobPatterns = new string[0]; // Empty array to skip ID-based parsing
            
            foreach (var pattern in jobPatterns)
            {
                var matches = Regex.Matches(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
                
                foreach (Match match in matches)
                {
                    if (match.Groups.Count >= 3 && 
                        byte.TryParse(match.Groups[1].Value, out var jobId) && 
                        short.TryParse(match.Groups[2].Value, out var level))
                    {
                        // Validate job ID and level ranges
                        if (!IsValidJobId(jobId))
                        {
                            _logger.LogWarning($"Invalid job ID {jobId} found in Lodestone data (valid range: {MIN_JOB_ID}-{MAX_JOB_ID})");
                            continue;
                        }

                        if (!IsValidJobLevel(level))
                        {
                            _logger.LogWarning($"Invalid job level {level} found for job {jobId} in Lodestone data (valid range: {MIN_JOB_LEVEL}-{MAX_JOB_LEVEL})");
                            continue;
                        }

                        // Only store valid job data
                        jobLevels[jobId] = level;
                        
                        // Track the highest level job as main job
                        if (!mainJobLevel.HasValue || level > mainJobLevel.Value)
                        {
                            mainJobId = jobId;
                            mainJobLevel = level;
                        }
                    }
                }
            }
            
            // Alternative: Look for specific class/job blocks with stricter constraints
            var jobBlockPattern = @"character__job[^>]*job--(\d{1,2})[^>]*>.*?character__job__level[^>]*>(\d{1,3})<";
            var jobBlockMatches = Regex.Matches(html, jobBlockPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            
            foreach (Match match in jobBlockMatches)
            {
                if (match.Groups.Count >= 3 && 
                    byte.TryParse(match.Groups[1].Value, out var jobId) && 
                    short.TryParse(match.Groups[2].Value, out var level))
                {
                    // Validate job ID and level ranges
                    if (!IsValidJobId(jobId))
                    {
                        _logger.LogWarning($"Invalid job ID {jobId} found in job block (valid range: {MIN_JOB_ID}-{MAX_JOB_ID})");
                        continue;
                    }

                    if (!IsValidJobLevel(level))
                    {
                        _logger.LogWarning($"Invalid job level {level} found for job {jobId} in job block (valid range: {MIN_JOB_LEVEL}-{MAX_JOB_LEVEL})");
                        continue;
                    }

                    // Only store valid job data (avoid duplicates)
                    if (!jobLevels.ContainsKey(jobId))
                    {
                        jobLevels[jobId] = level;
                        
                        if (!mainJobLevel.HasValue || level > mainJobLevel.Value)
                        {
                            mainJobId = jobId;
                            mainJobLevel = level;
                        }
                    }
                }
            }
            
            // Use text-based parsing to match exact Lodestone format: "80 Paladin 571,166 / 5,992,000"
            
            // Pattern for HTML structure: <div class="character__job__level">100</div><div class="character__job__name">Paladin</div>
            var textPatterns = new[]
            {
                // Primary pattern: Level div followed by name div (actual Lodestone HTML structure)
                @"<div\s+class=""character__job__level"">(\d{1,3})</div>.*?<div\s+class=""character__job__name[^""]*""[^>]*>([A-Za-z\s]+?)</div>",
                // Alternative pattern: Level and name in separate HTML tags
                @"character__job__level"">(\d{1,3})</[^>]+>.*?character__job__name[^>]*>([A-Za-z\s]+?)</",
                // Fallback pattern: Level followed by job name in any HTML structure
                @">(\d{1,3})<[^>]*>.*?js__tooltip[^>]*>([A-Za-z\s]+?)<"
            };
                
                foreach (var pattern in textPatterns)
                {
                    var matches = Regex.Matches(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
                    
                    foreach (Match match in matches)
                    {
                        if (match.Groups.Count >= 3 && 
                            short.TryParse(match.Groups[1].Value, out var level) &&
                            !string.IsNullOrWhiteSpace(match.Groups[2].Value))
                        {
                            var jobName = match.Groups[2].Value.Trim();
                            
                            // Clean up job name - remove extra whitespace and common artifacts
                            jobName = System.Text.RegularExpressions.Regex.Replace(jobName, @"\s+", " ");
                            jobName = jobName.Replace("  ", " ").Trim();
                            
                            // Try to map job name to ID
                            if (JobNameToId.TryGetValue(jobName, out var jobId))
                            {
                                if (IsValidJobLevel(level))
                                {
                                    jobLevels[jobId] = level;
                                    
                                    if (!mainJobLevel.HasValue || level > mainJobLevel.Value)
                                    {
                                        mainJobId = jobId;
                                        mainJobLevel = level;
                                    }
                                }
                                else
                                {
                                    _logger.LogWarning($"Invalid level {level} for job {jobName}");
                                }
                            }
                            else
                            {
                            }
                        }
                    }
                }
            
            
            return jobLevels.Count > 0 ? (jobLevels, mainJobId, mainJobLevel) : (null, null, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting job data from profile");
            return (null, null, null);
        }
    }

    /// <summary>
    /// Extracts minion collection data from character minion page HTML
    /// </summary>
    private List<MinionInfo>? ExtractMinionDataFromProfile(string html)
    {
        try
        {
            var minions = new List<MinionInfo>();
            
            // Updated patterns to match current Lodestone minion page structure
            // Try multiple patterns to handle different HTML structures
            var minionItemPattern = @"<li[^>]*class=""[^""]*minion__list_item[^""]*""[^>]*>.*?<img[^>]+src=""(https://lds-img\.finalfantasyxiv\.com/itemicon/[^""]+\.png[^""]*)""[^>]*alt=""([^""]+)""[^>]*>.*?</li>";
            // Alternative pattern for minion names (currently unused but available for future enhancement)
            // var minionNamePattern = @"<div[^>]*class=""[^""]*minion__name[^""]*""[^>]*>([^<]+)</div>";
            var minionTooltipPattern = @"data-tooltip=""([^""]+)""[^>]*><img[^>]+src=""(https://lds-img\.finalfantasyxiv\.com/itemicon/[^""]+\.png[^""]*)""";
            
            var itemMatches = Regex.Matches(html, minionItemPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            
            if (itemMatches.Count > 0)
            {
                _logger.LogInformation($"Found {itemMatches.Count} minion matches using primary pattern");
                foreach (Match match in itemMatches)
                {
                    if (match.Groups.Count >= 3)
                    {
                        var iconUrl = match.Groups[1].Value.Trim();
                        var minionName = match.Groups[2].Value.Trim();
                        
                        if (!string.IsNullOrEmpty(iconUrl))
                        {
                            var minion = new MinionInfo
                            {
                                Name = !string.IsNullOrEmpty(minionName) ? minionName : "Unknown Minion",
                                MinionId = null, // Would need game data mapping to get actual ID
                                AcquiredDate = null, // Not available from current Lodestone format
                                IconUrl = iconUrl
                            };
                            
                            minions.Add(minion);
                            _logger.LogDebug($"Added minion: {minionName} with icon: {iconUrl}");
                        }
                    }
                }
            }
            // Try tooltip pattern if primary pattern didn't work
            else
            {
                _logger.LogInformation("Primary pattern failed, trying tooltip pattern");
                var tooltipMatches = Regex.Matches(html, minionTooltipPattern, RegexOptions.IgnoreCase);
                
                if (tooltipMatches.Count > 0)
                {
                    _logger.LogInformation($"Found {tooltipMatches.Count} minion matches using tooltip pattern");
                    foreach (Match match in tooltipMatches)
                    {
                        if (match.Groups.Count >= 3)
                        {
                            var minionName = match.Groups[1].Value.Trim();
                            var iconUrl = match.Groups[2].Value.Trim();
                            
                            if (!string.IsNullOrEmpty(iconUrl))
                            {
                                var minion = new MinionInfo
                                {
                                    Name = !string.IsNullOrEmpty(minionName) ? minionName : "Unknown Minion",
                                    MinionId = null,
                                    AcquiredDate = null,
                                    IconUrl = iconUrl
                                };
                                
                                minions.Add(minion);
                                _logger.LogDebug($"Added minion from tooltip: {minionName} with icon: {iconUrl}");
                            }
                        }
                    }
                }
            }
            
            // Final fallback: just look for any minion icons if structured approaches fail
            if (minions.Count == 0)
            {
                _logger.LogWarning("All structured patterns failed, using fallback icon pattern");
                var minionIconPattern = @"<img[^>]+src=""(https://lds-img\.finalfantasyxiv\.com/itemicon/[^""]+\.png[^""]*)""[^>]*(?:alt=""([^""]*)""|title=""([^""]*)""|data-tooltip=""([^""]*)""|[^>]*)>";
                
                var iconMatches = Regex.Matches(html, minionIconPattern, RegexOptions.IgnoreCase);
                _logger.LogInformation($"Found {iconMatches.Count} minion icons using fallback pattern");
                
                foreach (Match match in iconMatches)
                {
                    if (match.Groups.Count >= 2)
                    {
                        var iconUrl = match.Groups[1].Value.Trim();
                        
                        // Try to get name from various attributes
                        var minionName = "";
                        for (int i = 2; i < match.Groups.Count; i++)
                        {
                            if (!string.IsNullOrEmpty(match.Groups[i].Value.Trim()))
                            {
                                minionName = match.Groups[i].Value.Trim();
                                break;
                            }
                        }
                        
                        if (!string.IsNullOrEmpty(iconUrl))
                        {
                            // If we still don't have a name, create a placeholder based on hash
                            if (string.IsNullOrEmpty(minionName))
                            {
                                var urlParts = iconUrl.Split('/');
                                var filename = urlParts.LastOrDefault()?.Split('?').FirstOrDefault()?.Replace(".png", "");
                                minionName = $"Minion #{filename?.Substring(0, Math.Min(8, filename?.Length ?? 0))}";
                            }
                            
                            var minion = new MinionInfo
                            {
                                Name = minionName,
                                MinionId = null,
                                AcquiredDate = null,
                                IconUrl = iconUrl
                            };
                            
                            minions.Add(minion);
                            _logger.LogDebug($"Added fallback minion: {minionName} with icon: {iconUrl}");
                        }
                    }
                }
            }
            
            _logger.LogInformation($"Extracted {minions.Count} minions from Lodestone profile");
            return minions.Count > 0 ? minions : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting minion data from profile");
            return null;
        }
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
    }
}