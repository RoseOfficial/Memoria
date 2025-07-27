using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
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
        _logger.LogInformation("LodestoneRefreshService.StartAsync() called");
        
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

        _logger.LogInformation("Starting LodestoneRefreshService...");
        _logger.LogInformation($"Configuration - Enabled: {Config.LodestoneRefreshEnabled}, " +
                             $"Processing: 1 player per second, " +
                             $"Stale Threshold: {Config.LodestoneStaleThresholdHours}h, " +
                             $"Idle Delay: {Config.LodestoneRefreshIdleDelaySeconds}s");
        
        try
        {
            // Populate initial queue with existing players
            _logger.LogInformation("Populating initial refresh queue...");
            await PopulateInitialQueue();
            
            // Start background processing
            _logger.LogInformation("Starting background processing task...");
            _processingTask = Task.Run(() => ProcessRefreshQueue(_cancellationTokenSource.Token));
            
            _logger.LogInformation("LodestoneRefreshService started successfully");
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
                _logger.LogDebug($"Queued NEW player {contentId} for priority Lodestone refresh (Priority queue size now: {_priorityQueue.Count})");
            }
            else
            {
                _refreshQueue.Enqueue(contentId);
                _logger.LogDebug($"Queued player {contentId} for Lodestone refresh (Queue size now: {_refreshQueue.Count})");
            }
        }
        // Remove the excessive "already queued" logging - this gets called every frame
    }

    /// <summary>
    /// Force processes the next player in queue immediately (for testing)
    /// </summary>
    public async Task<bool> ForceProcessNextPlayer()
    {
        _logger.LogInformation($"ForceProcessNextPlayer called - Priority queue: {_priorityQueue.Count}, Regular queue: {_refreshQueue.Count}");
        
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
            _logger.LogInformation("PopulateInitialQueue: Starting...");
            
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
                    _logger.LogDebug($"PopulateInitialQueue: Queued player {contentId}");
                }
                else
                {
                    _logger.LogDebug($"PopulateInitialQueue: Player {contentId} already queued");
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
                    _logger.LogDebug($"Processing priority player {contentId}");
                }
                else if (_refreshQueue.TryDequeue(out contentId))
                {
                    hasPlayer = true;
                    _logger.LogDebug($"Processing regular player {contentId}");
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
            _logger.LogDebug($"ProcessSinglePlayer: Starting refresh for player {contentId}");
            
            using var scope = _serviceProvider.CreateScope();
            using var dbContext = scope.ServiceProvider.GetRequiredService<RetainerTrackContext>();
            
            var player = await dbContext.Players.FindAsync(contentId);
            if (player == null)
            {
                _logger.LogWarning($"ProcessSinglePlayer: Player {contentId} not found during refresh processing");
                return;
            }

            _logger.LogInformation($"ProcessSinglePlayer: Refreshing Lodestone data for player {player.Name} ({contentId})");
            
            var success = await RefreshPlayerData(player, dbContext);
            if (success)
            {
                await dbContext.SaveChangesAsync();
                _logger.LogInformation($"ProcessSinglePlayer: Successfully refreshed data for player {player.Name}");
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
            _logger.LogInformation($"Fetching Lodestone data for player {player.Name}...");
            
            // Fetch avatar URL directly from Lodestone using character name and world
            if (string.IsNullOrEmpty(player.Name))
            {
                _logger.LogWarning($"Player {player.LocalContentId} has no name, skipping Lodestone refresh");
                player.LastScannedAt = DateTime.UtcNow;
                return true;
            }
            
            var avatarUrl = await FetchLodestoneAvatarUrl(player.Name, player.LocalContentId);
            
            var hasUpdates = false;
            
            if (!string.IsNullOrEmpty(avatarUrl) && avatarUrl != player.AvatarLink)
            {
                player.AvatarLink = avatarUrl;
                hasUpdates = true;
                
                // Update cached player data so UI reflects changes immediately
                PersistenceContext.UpdateCachedPlayerAvatar(player.LocalContentId, player.AvatarLink);
                
                // Clear any failed download attempts for this new URL
                Plugin.AvatarCacheManager.ClearFailedDownloads(player.AvatarLink);
                
                _logger.LogInformation($"Updated avatar for player {player.Name}: {player.AvatarLink}");
            }
            else if (string.IsNullOrEmpty(avatarUrl))
            {
                _logger.LogWarning($"Could not fetch avatar URL for player {player.Name}");
            }
            else
            {
                _logger.LogDebug($"Avatar URL unchanged for player {player.Name}");
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
    /// Fetches avatar URL directly from Lodestone using character search
    /// </summary>
    private async Task<string?> FetchLodestoneAvatarUrl(string characterName, ulong contentId)
    {
        try
        {
            using var httpClient = new System.Net.Http.HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "AlphaScope/1.0");
            
            // Search for character on Lodestone
            var searchUrl = $"https://na.finalfantasyxiv.com/lodestone/character/?q={Uri.EscapeDataString(characterName)}&worldname=";
            _logger.LogDebug($"Searching Lodestone: {searchUrl}");
            
            var searchResponse = await httpClient.GetStringAsync(searchUrl);
            
            // Parse search results to find the character's Lodestone ID
            var lodestoneId = ExtractLodestoneIdFromSearch(searchResponse, characterName);
            
            if (string.IsNullOrEmpty(lodestoneId))
            {
                _logger.LogWarning($"Could not find Lodestone ID for character {characterName}");
                return null;
            }
            
            // Fetch character profile page
            var profileUrl = $"https://na.finalfantasyxiv.com/lodestone/character/{lodestoneId}/";
            _logger.LogDebug($"Fetching profile: {profileUrl}");
            
            var profileResponse = await httpClient.GetStringAsync(profileUrl);
            _logger.LogInformation($"Profile HTML length: {profileResponse.Length} chars");
            
            // Log a snippet of the HTML to check if we're getting valid content
            var htmlSnippet = profileResponse.Length > 500 ? profileResponse.Substring(0, 500) : profileResponse;
            _logger.LogDebug($"HTML snippet: {htmlSnippet}...");
            
            // Extract avatar URL from profile page
            var avatarUrl = ExtractAvatarUrlFromProfile(profileResponse);
            
            if (!string.IsNullOrEmpty(avatarUrl))
            {
                _logger.LogInformation($"Found avatar URL for {characterName}: {avatarUrl}");
                return avatarUrl;
            }
            else
            {
                _logger.LogWarning($"Could not extract avatar URL from profile for {characterName}");
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error fetching Lodestone avatar for {characterName}");
            return null;
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
                _logger.LogDebug($"Using fallback Lodestone ID extraction for {characterName}");
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
            _logger.LogDebug($"Extracting avatar from HTML (length: {html.Length})");
            
            // Find all img tags and log them for debugging
            var allImgMatches = Regex.Matches(html, @"<img[^>]+src=""([^""]*)""\s*[^>]*>", RegexOptions.IgnoreCase);
            _logger.LogDebug($"Found {allImgMatches.Count} img tags in profile HTML");
            
            foreach (Match imgMatch in allImgMatches)
            {
                var imgUrl = imgMatch.Groups[1].Value;
                _logger.LogDebug($"Image found: {imgUrl}");
                
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
                    
                    _logger.LogInformation($"Found character avatar URL: {imgUrl}");
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