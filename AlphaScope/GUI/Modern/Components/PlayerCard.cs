using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using ImGuiNET;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Components;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using AlphaScope.GUI.Modern.Base;
using AlphaScope.Handlers;
using AlphaScope.GUI;

namespace AlphaScope.GUI.Modern.Components;

/// <summary>
/// Modern card component for displaying player information.
/// Features avatar, status indicators, job information, and quick actions.
/// </summary>
public class PlayerCard : BaseComponent
{
    private readonly PersistenceContext.CachedPlayer _cachedPlayer;
    private readonly ulong _contentId;
    private bool _isFavorited;
    private string? _avatarUrl;
    private bool _hasQueuedForRefresh = false;
    
    internal PersistenceContext.CachedPlayer Player => _cachedPlayer;
    public ulong ContentId => _contentId;

    internal PlayerCard(string id, ulong contentId, PersistenceContext.CachedPlayer cachedPlayer) 
        : base(id)
    {
        _contentId = contentId;
        _cachedPlayer = cachedPlayer ?? throw new ArgumentNullException(nameof(cachedPlayer));
        _isFavorited = Plugin.Instance.Configuration.FavoritedPlayer.ContainsKey((long)contentId);
        
        // Get avatar URL from cached player
        _avatarUrl = _cachedPlayer.AvatarLink;
    }

    protected override void OnRender()
    {
        var cardSize = ImGuiHelpers.ScaledVector2(320f, 120f); // Slightly wider to accommodate content
        
        using var childId = ImRaii.PushId(_id);
        
        // Use ImGui's built-in layout system instead of manual positioning
        using (var child = ImRaii.Child($"card_{_id}", cardSize, true, ImGuiWindowFlags.NoScrollbar))
        {
            if (!child) return;
            
            // Calculate available space for content
            var contentArea = ImGui.GetContentRegionAvail();
            var avatarSize = ImGuiHelpers.ScaledVector2(64f, 64f);
            var buttonAreaWidth = ImGuiHelpers.ScaledVector2(30f, 0f).X; // Approximate button area width
            var textAreaWidth = contentArea.X - avatarSize.X - buttonAreaWidth - (ImGui.GetStyle().ItemSpacing.X * 3);
            
            // Left side: Avatar placeholder and basic info
            RenderAvatar(avatarSize);
            
            ImGui.SameLine();
            
            // Player info column with clipped text
            ImGui.BeginGroup();
            
            // Player name with text clipping
            using (var nameColor = ThemeManager.PushColor(ImGuiCol.Text, ThemeManager.Colors.TextPrimary))
            {
                var playerName = _cachedPlayer.Name;
                var nameSize = ImGui.CalcTextSize(playerName);
                
                if (nameSize.X > textAreaWidth)
                {
                    // Truncate name with ellipsis if too long
                    var truncatedName = TruncateText(playerName, textAreaWidth - ImGui.CalcTextSize("...").X);
                    ImGui.Text($"{truncatedName}...");
                    
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(playerName); // Show full name on hover
                    }
                }
                else
                {
                    ImGui.Text(playerName);
                }
            }
            
            // World information with text clipping
            using (var worldColor = ThemeManager.PushColor(ImGuiCol.Text, ThemeManager.Colors.TextSecondary))
            {
                var homeWorldName = _cachedPlayer.HomeWorldId.HasValue ? Utils.GetWorldName(_cachedPlayer.HomeWorldId.Value) : "Unknown";
                var worldSize = ImGui.CalcTextSize(homeWorldName);
                
                if (worldSize.X > textAreaWidth)
                {
                    var truncatedWorld = TruncateText(homeWorldName, textAreaWidth - ImGui.CalcTextSize("...").X);
                    ImGui.Text($"{truncatedWorld}...");
                    
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(homeWorldName);
                    }
                }
                else
                {
                    ImGui.Text(homeWorldName);
                }
            }
            
            // Status with colored indicator
            var statusColor = GetStatusColor();
            using (var circleColor = ThemeManager.PushColor(ImGuiCol.Text, statusColor))
            {
                ImGui.Text("●");
            }
            
            ImGui.SameLine();
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() - ImGui.GetStyle().ItemSpacing.X);
            
            using (var textColor = ThemeManager.PushColor(ImGuiCol.Text, ThemeManager.Colors.TextMuted))
            {
                var statusText = $" {GetLastSeenText()}";
                var statusSize = ImGui.CalcTextSize(statusText);
                var availableStatusWidth = textAreaWidth - ImGui.CalcTextSize("● ").X;
                
                if (statusSize.X > availableStatusWidth)
                {
                    var truncatedStatus = TruncateText(statusText.Trim(), availableStatusWidth - ImGui.CalcTextSize("...").X);
                    ImGui.Text($" {truncatedStatus}...");
                    
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(statusText.Trim());
                    }
                }
                else
                {
                    ImGui.Text(statusText);
                }
            }
            
            ImGui.EndGroup();
            
            // Right side: Action buttons - positioned absolutely to prevent overflow
            var buttonStartX = contentArea.X - buttonAreaWidth + ImGui.GetStyle().WindowPadding.X;
            ImGui.SameLine();
            ImGui.SetCursorPosX(buttonStartX);
            
            ImGui.BeginGroup();
            
            // Favorite button
            var heartIcon = _isFavorited ? FontAwesomeIcon.Heart : FontAwesomeIcon.Heart;
            var heartColor = _isFavorited ? ThemeManager.Colors.Error : ThemeManager.Colors.TextMuted;
            
            using (var favColor = ThemeManager.PushColor(ImGuiCol.Text, heartColor))
            {
                if (ImGuiComponents.IconButton($"favorite_{_id}", heartIcon))
                {
                    ToggleFavorite();
                }
            }
            
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(_isFavorited ? "Remove from favorites" : "Add to favorites");
            }
            
            // Details button
            if (ImGuiComponents.IconButton($"details_{_id}", FontAwesomeIcon.InfoCircle))
            {
                OpenDetailsWindow();
            }
            
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("View detailed information");
            }
            
            // Adventure plate button
            if (ImGuiComponents.IconButton($"plate_{_id}", FontAwesomeIcon.AddressCard))
            {
                OpenAdventurePlate();
            }
            
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Open Adventure Plate");
            }
            
            ImGui.EndGroup();
        }
    }

    private void ToggleFavorite()
    {
        var config = Plugin.Instance.Configuration;
        var longContentId = (long)_contentId;
        
        if (_isFavorited)
        {
            config.FavoritedPlayer.TryRemove(longContentId, out _);
            _isFavorited = false;
        }
        else
        {
            // Create favorite data - simplified approach
            var favoriteData = new Configuration.CachedFavoritedPlayer 
            { 
                Name = _cachedPlayer.Name, 
                AccountId = _cachedPlayer.AccountId ?? 0,
                Note = ""
            };
            config.FavoritedPlayer[longContentId] = favoriteData;
            _isFavorited = true;
        }
        
        config.Save();
    }

    private void OpenDetailsWindow()
    {
        try
        {
            // Check if window already exists for this content ID
            var existingWindow = Plugin.Instance.ws.Windows
                .OfType<GUI.Modern.Views.PlayerDetailsWindow>()
                .FirstOrDefault(w => w.ContentId == _contentId);
            
            if (existingWindow != null)
            {
                // Reuse existing window
                existingWindow.IsOpen = true;
                existingWindow.BringToFront();
            }
            else
            {
                // Create new window
                var detailsWindow = new GUI.Modern.Views.PlayerDetailsWindow(_contentId, _cachedPlayer);
                Plugin.Instance.ws.AddWindow(detailsWindow);
                detailsWindow.IsOpen = true;
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"Failed to open details window for player {_contentId}: {ex}");
        }
    }

    private void OpenAdventurePlate()
    {
        try
        {
            // TODO: Implement adventure plate view in modern UI
            Plugin.Log.Info($"Opening adventure plate for player {_contentId} - Feature coming soon in modern UI");
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"Failed to open adventure plate for {_contentId}: {ex}");
        }
    }

    // Background refresh service handles avatar fetching - no manual fetching needed

    private string GetLastSeenText()
    {
        // First check if player has been scanned recently (for currently visible players)
        if (PersistenceContext._recentlyScannedPlayers.TryGetValue(_contentId, out var recentEntry))
        {
            var lastSeen = DateTimeOffset.FromUnixTimeSeconds(recentEntry.ScannedAt);
            var timeAgo = DateTimeOffset.Now - lastSeen;
            return $"Last seen {FormatTimeAgo(timeAgo)}";
        }
        
        // Fall back to cached player data for previously scanned players
        if (_cachedPlayer.LastScannedAt.HasValue)
        {
            var lastScanned = _cachedPlayer.LastScannedAt.Value;
            var timeAgo = DateTime.UtcNow - lastScanned;
            return $"Last seen {FormatTimeAgo(timeAgo)}";
        }
        
        return "Never seen";
    }

    private static string FormatTimeAgo(TimeSpan timeAgo)
    {
        if (timeAgo.TotalSeconds < 30) return "online now";
        if (timeAgo.TotalMinutes < 2) return "moments ago";
        if (timeAgo.TotalMinutes < 5) return "a few minutes ago";
        if (timeAgo.TotalMinutes < 15) return "recently";
        if (timeAgo.TotalMinutes < 30) return "half an hour ago";
        if (timeAgo.TotalHours < 1) return "less than an hour ago";
        if (timeAgo.TotalHours < 2) return "about an hour ago";
        if (timeAgo.TotalHours < 6) return "a few hours ago";
        if (timeAgo.TotalHours < 12) return "earlier today";
        if (timeAgo.TotalDays < 1) return "yesterday";
        if (timeAgo.TotalDays < 2) return "2 days ago";
        if (timeAgo.TotalDays < 7) return $"{(int)timeAgo.TotalDays} days ago";
        if (timeAgo.TotalDays < 14) return "about a week ago";
        if (timeAgo.TotalDays < 30) return $"{(int)(timeAgo.TotalDays / 7)} weeks ago";
        if (timeAgo.TotalDays < 60) return "about a month ago";
        if (timeAgo.TotalDays < 365) return $"{(int)(timeAgo.TotalDays / 30)} months ago";
        return "over a year ago";
    }

    private Vector4 GetStatusColor()
    {
        // First check if player has been scanned recently (for currently visible players)
        if (PersistenceContext._recentlyScannedPlayers.TryGetValue(_contentId, out var recentEntry))
        {
            var lastSeen = DateTimeOffset.FromUnixTimeSeconds(recentEntry.ScannedAt);
            var timeAgo = DateTimeOffset.Now - lastSeen;
            
            if (timeAgo.TotalHours < 1) return ThemeManager.Colors.Success; // Green for recent
            if (timeAgo.TotalDays < 1) return ThemeManager.Colors.Warning; // Yellow for moderate
        }
        // Fall back to cached player data for previously scanned players
        else if (_cachedPlayer.LastScannedAt.HasValue)
        {
            var lastScanned = _cachedPlayer.LastScannedAt.Value;
            var timeAgo = DateTime.UtcNow - lastScanned;
            
            if (timeAgo.TotalHours < 1) return ThemeManager.Colors.Success; // Green for recent
            if (timeAgo.TotalDays < 1) return ThemeManager.Colors.Warning; // Yellow for moderate
            if (timeAgo.TotalDays < 7) return ThemeManager.Colors.Warning; // Yellow for week old
        }
        
        return ThemeManager.Colors.Error; // Red for old/never seen
    }

    private void RenderAvatar(Vector2 avatarSize)
    {
        nint avatarHandle = 0;
        
        // Get fresh player data from cache every frame to pick up background updates
        if (PersistenceContext._playerCache.TryGetValue(_contentId, out var freshPlayerData))
        {
            // Check if we got a new avatar URL from the background service
            if (!string.IsNullOrEmpty(freshPlayerData.AvatarLink) && _avatarUrl != freshPlayerData.AvatarLink)
            {
                _avatarUrl = freshPlayerData.AvatarLink;
                _hasQueuedForRefresh = false; // Reset flag when we get a new avatar URL
            }
        }
        
        // Also check the original cached player reference (fallback)
        if (!string.IsNullOrEmpty(_cachedPlayer.AvatarLink) && _avatarUrl != _cachedPlayer.AvatarLink)
        {
            _avatarUrl = _cachedPlayer.AvatarLink;
            _hasQueuedForRefresh = false; // Reset flag when we get a new avatar URL
        }
        
        // Try to get avatar from cache if URL is available
        if (!string.IsNullOrEmpty(_avatarUrl))
        {
            avatarHandle = Plugin.AvatarCacheManager.GetAvatarHandle(_avatarUrl);
        }
        else
        {
        }
        
        if (avatarHandle != 0)
        {
            // Display the actual avatar image
            ImGui.Image(avatarHandle, avatarSize);
        }
        else
        {
            // Fall back to placeholder button
            ImGui.Button("👤", avatarSize);
            
            // Queue player for background refresh if no avatar available (only once)
            if (string.IsNullOrEmpty(_avatarUrl) && !_hasQueuedForRefresh)
            {
                PersistenceContext.QueuePlayerForLodestoneRefresh(_contentId);
                _hasQueuedForRefresh = true;
            }
        }
    }

    private string TruncateText(string text, float maxWidth)
    {
        if (string.IsNullOrEmpty(text)) return text;
        
        // Binary search to find the longest substring that fits
        int left = 0;
        int right = text.Length;
        string result = text;
        
        while (left <= right)
        {
            int mid = (left + right) / 2;
            var candidate = text.Substring(0, mid);
            var size = ImGui.CalcTextSize(candidate);
            
            if (size.X <= maxWidth)
            {
                result = candidate;
                left = mid + 1;
            }
            else
            {
                right = mid - 1;
            }
        }
        
        return result;
    }
}