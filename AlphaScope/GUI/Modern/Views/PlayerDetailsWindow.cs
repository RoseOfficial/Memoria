using System;
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
using AlphaScope.Database;
using Microsoft.Extensions.DependencyInjection;

namespace AlphaScope.GUI.Modern.Views;

/// <summary>
/// Modern player details window showing comprehensive player information.
/// Displays player data, statistics, and actions in a clean, modern interface.
/// </summary>
public class PlayerDetailsWindow : BaseModernWindow
{
    private readonly ulong _contentId;
    private readonly PersistenceContext.CachedPlayer _cachedPlayer;
    private bool _isFavorited;
    private DateTime? _lastScannedAt;
    private bool _isRefreshing = false;
    private string? _avatarUrl;
    private bool _hasQueuedForRefresh = false;
    
    /// <summary>
    /// Public getter for the content ID to allow window identification
    /// </summary>
    public ulong ContentId => _contentId;
    
    internal PlayerDetailsWindow(ulong contentId, PersistenceContext.CachedPlayer cachedPlayer) 
        : base($"PlayerDetails_{contentId}", $"Player Details - {cachedPlayer.Name}")
    {
        _contentId = contentId;
        _cachedPlayer = cachedPlayer ?? throw new ArgumentNullException(nameof(cachedPlayer));
        _isFavorited = Plugin.Instance.Configuration.FavoritedPlayer.ContainsKey((long)contentId);
        
        // Load LastScannedAt from database
        _lastScannedAt = GetLastScannedAt();
        
        // Get avatar URL from cached player
        _avatarUrl = _cachedPlayer.AvatarLink;
        
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(500, 400),
            MaximumSize = new Vector2(800, 600)
        };
        
        // Set default size
        Size = new Vector2(600, 500);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    protected override void OnDraw()
    {
        DrawPlayerHeader();
        
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(5f);
        
        // Main content area with tabs
        if (ImGui.BeginTabBar("PlayerDetailsTabs"))
        {
            if (ImGui.BeginTabItem("Overview"))
            {
                DrawOverviewTab();
                ImGui.EndTabItem();
            }
            
            if (ImGui.BeginTabItem("Statistics"))
            {
                DrawStatisticsTab();
                ImGui.EndTabItem();
            }
            
            if (ImGui.BeginTabItem("History"))
            {
                DrawHistoryTab();
                ImGui.EndTabItem();
            }
            
            if (ImGui.BeginTabItem("Notes"))
            {
                DrawNotesTab();
                ImGui.EndTabItem();
            }
            
            ImGui.EndTabBar();
        }
    }

    private void DrawPlayerHeader()
    {
        // Avatar placeholder and basic info
        var avatarSize = ImGuiHelpers.ScaledVector2(80f, 80f);
        
        // Left side: Avatar
        RenderAvatar(avatarSize);
        
        ImGui.SameLine();
        
        // Middle: Player info
        ImGui.BeginGroup();
        
        // Player name (large)
        using (var nameFont = ImRaii.PushFont(UiBuilder.MonoFont))
        using (var nameColor = ThemeManager.PushColor(ImGuiCol.Text, ThemeManager.Colors.TextPrimary))
        {
            ImGui.Text(_cachedPlayer.Name);
        }
        
        // Content ID
        using (var idColor = ThemeManager.PushColor(ImGuiCol.Text, ThemeManager.Colors.TextSecondary))
        {
            ImGui.Text($"Content ID: {_contentId}");
        }
        
        // Account ID if available
        if (_cachedPlayer.AccountId.HasValue)
        {
            using (var accountColor = ThemeManager.PushColor(ImGuiCol.Text, ThemeManager.Colors.TextSecondary))
            {
                ImGui.Text($"Account ID: {_cachedPlayer.AccountId.Value}");
            }
        }
        
        // Last seen info with colored indicator
        var lastSeenText = GetLastSeenText();
        var statusColor = GetStatusColor();
        using (var circleColor = ThemeManager.PushColor(ImGuiCol.Text, statusColor))
        {
            ImGui.Text("●");
        }
        
        ImGui.SameLine();
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() - ImGui.GetStyle().ItemSpacing.X);
        
        using (var textColor = ThemeManager.PushColor(ImGuiCol.Text, ThemeManager.Colors.TextMuted))
        {
            ImGui.Text($" {lastSeenText}");
        }
        
        ImGui.EndGroup();
        
        // Right side: Action buttons
        ImGui.SameLine();
        ImGui.SetCursorPosX(ImGui.GetWindowWidth() - 120f * ImGuiHelpers.GlobalScale);
        
        ImGui.BeginGroup();
        
        // Favorite button
        var heartIcon = _isFavorited ? FontAwesomeIcon.Heart : FontAwesomeIcon.Heart;
        var heartColor = _isFavorited ? ThemeManager.Colors.Error : ThemeManager.Colors.TextMuted;
        
        using (var favColor = ThemeManager.PushColor(ImGuiCol.Text, heartColor))
        {
            if (ImGuiComponents.IconButton("favorite", heartIcon))
            {
                ToggleFavorite();
            }
        }
        
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(_isFavorited ? "Remove from favorites" : "Add to favorites");
        }
        
        // Adventure plate button
        if (ImGuiComponents.IconButton("plate", FontAwesomeIcon.AddressCard))
        {
            OpenAdventurePlate();
        }
        
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Open Adventure Plate (Coming Soon)");
        }
        
        // Export button
        if (ImGuiComponents.IconButton("export", FontAwesomeIcon.Download))
        {
            ExportPlayerData();
        }
        
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Export Player Data");
        }
        
        ImGui.EndGroup();
    }

    private void DrawOverviewTab()
    {
        // Basic Information
        if (ImGui.CollapsingHeader("Basic Information", ImGuiTreeNodeFlags.DefaultOpen))
        {
            DrawInfoRow("Name", _cachedPlayer.Name);
            DrawInfoRow("Content ID", _contentId.ToString());
            
            if (_cachedPlayer.AccountId.HasValue)
            {
                DrawInfoRow("Account ID", _cachedPlayer.AccountId.Value.ToString());
            }
            else
            {
                DrawInfoRow("Account ID", "Unknown");
            }
            
            DrawInfoRow("Status", _isFavorited ? "⭐ Favorited" : "Standard");
            DrawInfoRow("Last Seen", GetLastSeenText());
            DrawInfoRow("Last Scanned", GetLastScannedText());
        }
        
        ImGuiHelpers.ScaledDummy(10f);
        
        // Quick Actions
        if (ImGui.CollapsingHeader("Quick Actions", ImGuiTreeNodeFlags.DefaultOpen))
        {
            if (ImGui.Button("Copy Content ID"))
            {
                ImGui.SetClipboardText(_contentId.ToString());
                ShowNotification("Content ID copied to clipboard!");
            }
            
            ImGui.SameLine();
            
            if (_cachedPlayer.AccountId.HasValue)
            {
                if (ImGui.Button("Copy Account ID"))
                {
                    ImGui.SetClipboardText(_cachedPlayer.AccountId.Value.ToString());
                    ShowNotification("Account ID copied to clipboard!");
                }
            }
            else
            {
                using (ImRaii.Disabled(true))
                {
                    ImGui.Button("Copy Account ID");
                }
            }
            
            if (ImGui.Button("Search Similar"))
            {
                ShowComingSoon("Similar player search");
            }
            
            // Refresh button
            if (_isRefreshing)
            {
                using (ImRaii.Disabled(true))
                {
                    ImGui.Button("Refreshing...");
                }
            }
            else
            {
                if (ImGui.Button("Refresh Lodestone Data"))
                {
                    _ = Task.Run(async () =>
                    {
                        _isRefreshing = true;
                        try
                        {
                            await PersistenceContext.RefreshPlayerImmediately(_contentId);
                            _lastScannedAt = GetLastScannedAt(); // Refresh the timestamp
                        }
                        finally
                        {
                            _isRefreshing = false;
                        }
                    });
                }
            }
            
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Fetch fresh data from Lodestone immediately");
            }
        }
    }

    private void DrawStatisticsTab()
    {
        using (var color = ThemeManager.PushColor(ImGuiCol.Text, ThemeManager.Colors.TextMuted))
        {
            ImGui.Text("Player statistics and analytics coming soon...");
            ImGui.Text("This will include:");
            ImGui.BulletText("Encounter frequency");
            ImGui.BulletText("Time-based activity patterns");
            ImGui.BulletText("World/server presence");
            ImGui.BulletText("Job/class information");
        }
    }

    private void DrawHistoryTab()
    {
        if (ImGui.CollapsingHeader("Recent Encounters", ImGuiTreeNodeFlags.DefaultOpen))
        {
            // Check if player is in recent encounters
            if (PersistenceContext._recentlyScannedPlayers.TryGetValue(_contentId, out var recentEntry))
            {
                var lastSeen = DateTimeOffset.FromUnixTimeSeconds(recentEntry.ScannedAt);
                ImGui.Text($"Last encountered: {lastSeen.LocalDateTime:yyyy-MM-dd HH:mm:ss}");
                
                var timeAgo = DateTimeOffset.Now - lastSeen;
                ImGui.Text($"Time ago: {FormatTimeAgo(timeAgo)}");
            }
            else
            {
                using (var color = ThemeManager.PushColor(ImGuiCol.Text, ThemeManager.Colors.TextMuted))
                {
                    ImGui.Text("No recent encounter data available");
                }
            }
        }
        
        ImGuiHelpers.ScaledDummy(10f);
        
        using (var color = ThemeManager.PushColor(ImGuiCol.Text, ThemeManager.Colors.TextMuted))
        {
            ImGui.Text("Detailed history tracking coming soon...");
            ImGui.Text("This will include:");
            ImGui.BulletText("All encounter timestamps");
            ImGui.BulletText("Location/zone information");
            ImGui.BulletText("Activity context");
        }
    }

    private void DrawNotesTab()
    {
        // Get current note
        var config = Plugin.Instance.Configuration;
        var note = "";
        
        if (config.FavoritedPlayer.TryGetValue((long)_contentId, out var favoriteData))
        {
            note = favoriteData.Note ?? "";
        }
        
        ImGui.Text("Personal Notes:");
        ImGuiHelpers.ScaledDummy(5f);
        
        if (ImGui.InputTextMultiline("##PlayerNotes", ref note, 1000, new Vector2(-1, 200)))
        {
            // Save note (create favorite entry if needed)
            if (!config.FavoritedPlayer.ContainsKey((long)_contentId))
            {
                config.FavoritedPlayer[(long)_contentId] = new Configuration.CachedFavoritedPlayer
                {
                    Name = _cachedPlayer.Name,
                    AccountId = _cachedPlayer.AccountId ?? 0,
                    Note = note
                };
                _isFavorited = true;
            }
            else
            {
                config.FavoritedPlayer[(long)_contentId].Note = note;
            }
            
            config.Save();
        }
        
        ImGuiHelpers.ScaledDummy(10f);
        
        if (ImGui.Button("Clear Notes"))
        {
            if (config.FavoritedPlayer.TryGetValue((long)_contentId, out var existingFavorite))
            {
                existingFavorite.Note = "";
                config.Save();
            }
        }
    }

    private void DrawInfoRow(string label, string value)
    {
        ImGui.Text($"{label}:");
        ImGui.SameLine(150f * ImGuiHelpers.GlobalScale);
        using (var color = ThemeManager.PushColor(ImGuiCol.Text, ThemeManager.Colors.TextSecondary))
        {
            ImGui.Text(value);
        }
    }

    private string GetLastSeenText()
    {
        if (PersistenceContext._recentlyScannedPlayers.TryGetValue(_contentId, out var recentEntry))
        {
            var lastSeen = DateTimeOffset.FromUnixTimeSeconds(recentEntry.ScannedAt);
            var timeAgo = DateTimeOffset.Now - lastSeen;
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
        if (PersistenceContext._recentlyScannedPlayers.TryGetValue(_contentId, out var recentEntry))
        {
            var lastSeen = DateTimeOffset.FromUnixTimeSeconds(recentEntry.ScannedAt);
            var timeAgo = DateTimeOffset.Now - lastSeen;
            
            if (timeAgo.TotalHours < 1) return ThemeManager.Colors.Success; // Green for recent
            if (timeAgo.TotalDays < 1) return ThemeManager.Colors.Warning; // Yellow for moderate
        }
        
        return ThemeManager.Colors.Error; // Red for old/never seen
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

    private void OpenAdventurePlate()
    {
        ShowComingSoon("Adventure Plate viewer");
    }

    private void ExportPlayerData()
    {
        try
        {
            var exportData = $"Player: {_cachedPlayer.Name}\n" +
                           $"Content ID: {_contentId}\n" +
                           $"Account ID: {_cachedPlayer.AccountId?.ToString() ?? "Unknown"}\n" +
                           $"Last Seen: {GetLastSeenText()}\n" +
                           $"Favorited: {_isFavorited}\n" +
                           $"Export Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
            
            ImGui.SetClipboardText(exportData);
            ShowNotification("Player data exported to clipboard!");
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"Failed to export player data: {ex}");
            ShowNotification("Failed to export player data");
        }
    }

    private void ShowComingSoon(string feature)
    {
        Plugin.Log.Info($"{feature} - Coming soon in modern UI");
    }

    private void ShowNotification(string message)
    {
        Plugin.Log.Info($"[PlayerDetails] {message}");
    }

    private DateTime? GetLastScannedAt()
    {
        try
        {
            if (Plugin._serviceProvider == null) return null;
            
            using var scope = Plugin._serviceProvider.CreateScope();
            using var dbContext = scope.ServiceProvider.GetRequiredService<RetainerTrackContext>();
            var player = dbContext.Players.Find(_contentId);
            return player?.LastScannedAt;
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"Error retrieving LastScannedAt for player {_contentId}: {ex}");
            return null;
        }
    }

    private string GetLastScannedText()
    {
        if (_lastScannedAt == null)
        {
            return "Never scanned";
        }

        var timeAgo = DateTime.UtcNow - _lastScannedAt.Value;
        return $"Scanned {FormatTimeAgo(timeAgo)}";
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
        
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(string.IsNullOrEmpty(_avatarUrl) ? "Loading avatar..." : "Player Avatar");
        }
    }
}