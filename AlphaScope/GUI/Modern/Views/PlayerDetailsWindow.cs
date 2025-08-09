using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Components;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using AlphaScope.GUI.Modern.Base;
using AlphaScope.Handlers;
using AlphaScope.GUI;
using AlphaScope.Database;
using AlphaScope.Services;
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
        
        // Load LastScannedAt from cached player data
        _lastScannedAt = _cachedPlayer.LastScannedAt;
        
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
            
            if (ImGui.BeginTabItem("Jobs"))
            {
                DrawJobsTab();
                ImGui.EndTabItem();
            }
            
            if (ImGui.BeginTabItem("Minions"))
            {
                DrawMinionsTab();
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
            
            DrawInfoRow("Status", _isFavorited ? "Favorited" : "Standard");
            
            // World information - always show separate fields
            var homeWorldName = _cachedPlayer.HomeWorldId.HasValue ? Utils.GetWorldName(_cachedPlayer.HomeWorldId.Value) : "Unknown";
            var currentWorldName = _cachedPlayer.CurrentWorldId.HasValue ? Utils.GetWorldName(_cachedPlayer.CurrentWorldId.Value) : "Unknown";
            
            DrawInfoRow("Home World", homeWorldName);
            DrawInfoRow("Current World", currentWorldName);
            
            DrawInfoRow("Last Seen", GetLastSeenText());
            DrawInfoRow("Last Scanned", GetLastScannedText());
            
            // Main job information from Lodestone
            if (PersistenceContext._playerCache.TryGetValue(_contentId, out var freshPlayerData) && 
                freshPlayerData.MainJobId.HasValue && freshPlayerData.MainJobLevel.HasValue)
            {
                var mainJobName = Utils.GetJobName(freshPlayerData.MainJobId.Value);
                DrawInfoRow("Main Job (Lodestone)", $"{mainJobName} Lv.{freshPlayerData.MainJobLevel.Value}");
            }
            else
            {
                DrawInfoRow("Main Job (Lodestone)", "Not available");
            }
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
                            // Refresh cached player data and timestamp
                            if (PersistenceContext._playerCache.TryGetValue(_contentId, out var refreshedPlayer))
                            {
                                _lastScannedAt = refreshedPlayer.LastScannedAt;
                            }
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

    private void DrawMinionsTab()
    {
        // Get minion data from the cached player or fresh data
        List<MinionInfo>? minions = null;
        DateTime? lastMinionsDataUpdate = null;

        // Try to get fresh player data from cache first
        if (PersistenceContext._playerCache.TryGetValue(_contentId, out var freshPlayerData))
        {
            Plugin.Log.Information($"[MinionTab Debug] Found cached player data for {_contentId}, LodestoneMinionsData length: {freshPlayerData.LodestoneMinionsData?.Length ?? 0}");
            if (!string.IsNullOrEmpty(freshPlayerData.LodestoneMinionsData))
            {
                try
                {
                    minions = JsonSerializer.Deserialize<List<MinionInfo>>(freshPlayerData.LodestoneMinionsData);
                    lastMinionsDataUpdate = freshPlayerData.LastMinionsDataUpdate;
                    Plugin.Log.Information($"[MinionTab Debug] Successfully deserialized {minions?.Count ?? 0} minions for player {_contentId}");
                }
                catch (JsonException ex)
                {
                    Plugin.Log.Warning($"Failed to parse minion data for player {_contentId}: {ex.Message}");
                }
            }
            else
            {
                Plugin.Log.Information($"[MinionTab Debug] No minion data found for cached player {_contentId}");
            }
        }
        else
        {
            Plugin.Log.Information($"[MinionTab Debug] No cached player data found for {_contentId}");
        }

        // Minion Data Header
        if (ImGui.CollapsingHeader("Minion Collection", ImGuiTreeNodeFlags.DefaultOpen))
        {
            if (minions != null && minions.Count > 0)
            {
                // Collection Statistics
                ImGui.Text("Collection Overview:");
                ImGui.SameLine(150f * ImGuiHelpers.GlobalScale);
                using (var color = ThemeManager.PushColor(ImGuiCol.Text, ThemeManager.Colors.TextPrimary))
                {
                    ImGui.Text($"{minions.Count} minions collected");
                }

                ImGuiHelpers.ScaledDummy(5f);
                
                // Last Updated
                if (lastMinionsDataUpdate.HasValue)
                {
                    ImGui.Text("Last Updated:");
                    ImGui.SameLine(150f * ImGuiHelpers.GlobalScale);
                    using (var color = ThemeManager.PushColor(ImGuiCol.Text, ThemeManager.Colors.TextSecondary))
                    {
                        var timeAgo = DateTime.UtcNow - lastMinionsDataUpdate.Value;
                        ImGui.Text(FormatTimeAgo(timeAgo));
                    }
                }

                ImGuiHelpers.ScaledDummy(10f);

                // Minion List with improved formatting
                using (var headerColor = ThemeManager.PushColor(ImGuiCol.Text, ThemeManager.Colors.TextPrimary))
                {
                    ImGui.Text("Collected Minions:");
                }
                ImGuiHelpers.ScaledDummy(8f);

                // Enhanced table with better styling
                var tableFlags = ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable | 
                                ImGuiTableFlags.Sortable | ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.ScrollY;
                
                if (ImGui.BeginTable("MinionsTable", 3, tableFlags, new Vector2(0, Math.Min(400f, minions.Count * 40f + 60f))))
                {
                    ImGui.TableSetupColumn("Icon", ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoSort, 48f * ImGuiHelpers.GlobalScale);
                    ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch, 0f);
                    ImGui.TableSetupColumn("How to Get", ImGuiTableColumnFlags.WidthFixed, 160f * ImGuiHelpers.GlobalScale);
                    ImGui.TableSetupScrollFreeze(0, 1);
                    
                    // Custom header styling
                    using (var headerBg = ThemeManager.PushColor(ImGuiCol.TableHeaderBg, ThemeManager.Colors.Surface))
                    using (var headerText = ThemeManager.PushColor(ImGuiCol.Text, ThemeManager.Colors.TextPrimary))
                    {
                        ImGui.TableHeadersRow();
                    }

                    // Sort minions alphabetically by name
                    var sortedMinions = minions.OrderBy(m => m.Name ?? "Unknown").ToList();

                    foreach (var minion in sortedMinions)
                    {
                        ImGui.TableNextRow();
                        var rowHeight = 40f * ImGuiHelpers.GlobalScale;
                        
                        // Icon column with better centering
                        ImGui.TableSetColumnIndex(0);
                        var iconSize = ImGuiHelpers.ScaledVector2(36f, 36f);
                        var cursorPos = ImGui.GetCursorPos();
                        ImGui.SetCursorPosY(cursorPos.Y + (rowHeight - iconSize.Y) / 2);
                        RenderMinionIcon(minion.IconUrl, iconSize);

                        // Name column with better formatting
                        ImGui.TableSetColumnIndex(1);
                        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + (rowHeight - ImGui.GetTextLineHeight()) / 2);
                        
                        var displayName = GetMinionDisplayName(minion);
                        var isPlaceholderName = displayName.StartsWith("[Unknown") || displayName.StartsWith("Minion #") || displayName.Contains(".png");
                        
                        using (var color = ThemeManager.PushColor(ImGuiCol.Text, isPlaceholderName ? ThemeManager.Colors.TextMuted : ThemeManager.Colors.TextPrimary))
                        {
                            ImGui.Text(displayName);
                        }

                        // How to Get column with improved styling
                        ImGui.TableSetColumnIndex(2);
                        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + (rowHeight - ImGui.GetTextLineHeight()) / 2);
                        
                        var acquisitionMethod = Utils.GetMinionAcquisitionMethod(minion.Name);
                        using (var color = ThemeManager.PushColor(ImGuiCol.Text, GetAcquisitionMethodColor(acquisitionMethod)))
                        {
                            ImGui.Text(acquisitionMethod);
                            
                            // Add tooltip for more information
                            if (ImGui.IsItemHovered())
                            {
                                ImGui.SetTooltip(GetAcquisitionTooltip(acquisitionMethod));
                            }
                        }
                    }

                    ImGui.EndTable();
                }

                ImGuiHelpers.ScaledDummy(10f);

                // Collection Statistics
                if (ImGui.CollapsingHeader("Collection Statistics"))
                {
                    var totalMinions = minions.Count;
                    var minionsWithIcons = minions.Count(m => !string.IsNullOrEmpty(m.IconUrl));

                    DrawInfoRow("Total Collected", totalMinions.ToString());
                    DrawInfoRow("With Icon Data", minionsWithIcons.ToString());
                    
                    if (totalMinions > 0)
                    {
                        var completionRate = (float)minionsWithIcons / totalMinions * 100f;
                        DrawInfoRow("Icon Coverage", $"{completionRate:F1}%");
                    }
                }
            }
            else
            {
                // Enhanced empty state with better visual feedback
                ImGuiHelpers.ScaledDummy(20f);
                
                // Center the empty state content
                var windowWidth = ImGui.GetContentRegionAvail().X;
                var emptyStateWidth = 300f * ImGuiHelpers.GlobalScale;
                var centerX = (windowWidth - emptyStateWidth) / 2;
                
                if (centerX > 0)
                {
                    ImGui.SetCursorPosX(ImGui.GetCursorPosX() + centerX);
                }
                
                ImGui.BeginGroup();
                
                // Large minion emoji as visual indicator
                using (var iconColor = ThemeManager.PushColor(ImGuiCol.Text, ThemeManager.Colors.TextMuted))
                {
                    var emojiSize = ImGui.CalcTextSize("🐾");
                    ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (emptyStateWidth - emojiSize.X) / 2);
                    ImGui.Text("🐾");
                }
                
                ImGuiHelpers.ScaledDummy(10f);
                
                // Primary message
                using (var titleColor = ThemeManager.PushColor(ImGuiCol.Text, ThemeManager.Colors.TextPrimary))
                {
                    var titleText = "No Minion Data Available";
                    var titleSize = ImGui.CalcTextSize(titleText);
                    ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (emptyStateWidth - titleSize.X) / 2);
                    ImGui.Text(titleText);
                }
                
                ImGuiHelpers.ScaledDummy(5f);
                
                // Secondary message
                using (var descColor = ThemeManager.PushColor(ImGuiCol.Text, ThemeManager.Colors.TextMuted))
                {
                    var descText1 = "Minion data will be collected when this";
                    var descText2 = "player's Lodestone profile is refreshed.";
                    
                    var desc1Size = ImGui.CalcTextSize(descText1);
                    var desc2Size = ImGui.CalcTextSize(descText2);
                    
                    ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (emptyStateWidth - desc1Size.X) / 2);
                    ImGui.Text(descText1);
                    ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (emptyStateWidth - desc2Size.X) / 2);
                    ImGui.Text(descText2);
                }
                
                ImGui.EndGroup();
                
                ImGuiHelpers.ScaledDummy(20f);

                if (ImGui.Button("Refresh Lodestone Data"))
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await PersistenceContext.RefreshPlayerImmediately(_contentId);
                        }
                        catch (Exception ex)
                        {
                            Plugin.Log.Error($"Failed to refresh player data: {ex}");
                        }
                    });
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Fetch minion data from Lodestone immediately");
                }
            }
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

    private void DrawJobsTab()
    {
        // Get job data from the cached player or fresh data
        Dictionary<byte, short>? jobLevels = null;
        byte? mainJobId = null;
        short? mainJobLevel = null;
        DateTime? lastJobDataUpdate = null;

        // Try to get fresh player data from cache first
        if (PersistenceContext._playerCache.TryGetValue(_contentId, out var freshPlayerData))
        {
            if (!string.IsNullOrEmpty(freshPlayerData.LodestoneJobData))
            {
                try
                {
                    var rawJobLevels = JsonSerializer.Deserialize<Dictionary<byte, short>>(freshPlayerData.LodestoneJobData);
                    if (rawJobLevels != null)
                    {
                        // Validate and filter job data
                        jobLevels = new Dictionary<byte, short>();
                        foreach (var kvp in rawJobLevels)
                        {
                            // Validate job ID (1-100) and level (1-100)
                            if (kvp.Key >= 1 && kvp.Key <= 100 && kvp.Value >= 1 && kvp.Value <= 100)
                            {
                                jobLevels[kvp.Key] = kvp.Value;
                            }
                            else
                            {
                                Plugin.Log.Warning($"Filtered invalid job data for player {_contentId}: Job ID {kvp.Key}, Level {kvp.Value}");
                            }
                        }
                    }
                    
                    // Validate main job data
                    if (freshPlayerData.MainJobId.HasValue && 
                        (freshPlayerData.MainJobId.Value < 1 || freshPlayerData.MainJobId.Value > 100))
                    {
                        Plugin.Log.Warning($"Invalid main job ID {freshPlayerData.MainJobId.Value} for player {_contentId}");
                        mainJobId = null;
                        mainJobLevel = null;
                    }
                    else if (freshPlayerData.MainJobLevel.HasValue && 
                             (freshPlayerData.MainJobLevel.Value < 1 || freshPlayerData.MainJobLevel.Value > 100))
                    {
                        Plugin.Log.Warning($"Invalid main job level {freshPlayerData.MainJobLevel.Value} for player {_contentId}");
                        mainJobLevel = null;
                    }
                    else
                    {
                        mainJobId = freshPlayerData.MainJobId;
                        mainJobLevel = freshPlayerData.MainJobLevel;
                    }
                    
                    lastJobDataUpdate = freshPlayerData.LastJobDataUpdate;
                }
                catch (JsonException ex)
                {
                    Plugin.Log.Warning($"Failed to parse job data for player {_contentId}: {ex.Message}");
                }
            }
        }

        // Job Data Header
        if (ImGui.CollapsingHeader("Job Information", ImGuiTreeNodeFlags.DefaultOpen))
        {
            if (jobLevels != null && jobLevels.Count > 0)
            {
                // Main Job Information
                if (mainJobId.HasValue && mainJobLevel.HasValue)
                {
                    ImGui.Text("Main Job:");
                    ImGui.SameLine(150f * ImGuiHelpers.GlobalScale);
                    using (var color = ThemeManager.PushColor(ImGuiCol.Text, ThemeManager.Colors.TextPrimary))
                    {
                        var mainJobName = Utils.GetJobName(mainJobId.Value);
                        ImGui.Text($"{mainJobName} Lv.{mainJobLevel.Value}");
                    }
                }

                ImGuiHelpers.ScaledDummy(5f);
                
                // Last Updated
                if (lastJobDataUpdate.HasValue)
                {
                    ImGui.Text("Last Updated:");
                    ImGui.SameLine(150f * ImGuiHelpers.GlobalScale);
                    using (var color = ThemeManager.PushColor(ImGuiCol.Text, ThemeManager.Colors.TextSecondary))
                    {
                        var timeAgo = DateTime.UtcNow - lastJobDataUpdate.Value;
                        ImGui.Text(FormatTimeAgo(timeAgo));
                    }
                }

                ImGuiHelpers.ScaledDummy(10f);

                // Job Levels by Role
                ImGui.Text("All Job Levels:");
                ImGuiHelpers.ScaledDummy(5f);

                // Group jobs by role
                var jobsByRole = jobLevels.GroupBy(j => Utils.GetJobRole(j.Key))
                    .OrderBy(g => g.Key switch
                    {
                        Utils.JobRole.Tank => 0,
                        Utils.JobRole.Healer => 1,
                        Utils.JobRole.MeleeDPS => 2,
                        Utils.JobRole.PhysicalRangedDPS => 3,
                        Utils.JobRole.MagicalRangedDPS => 4,
                        Utils.JobRole.Crafter => 5,
                        Utils.JobRole.Gatherer => 6,
                        _ => 7
                    });

                foreach (var roleGroup in jobsByRole)
                {
                    var role = roleGroup.Key;
                    var roleDisplayName = Utils.GetJobRoleDisplayName(role);
                    var roleColor = Utils.GetJobRoleColor(role);

                    // Role header with color
                    using (var headerColor = ThemeManager.PushColor(ImGuiCol.Text, roleColor))
                    {
                        if (ImGui.CollapsingHeader(roleDisplayName, ImGuiTreeNodeFlags.DefaultOpen))
                        {
                            if (ImGui.BeginTable($"JobTable_{role}", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable))
                            {
                                ImGui.TableSetupColumn("Icon", ImGuiTableColumnFlags.WidthFixed, 32f * ImGuiHelpers.GlobalScale);
                                ImGui.TableSetupColumn("Job", ImGuiTableColumnFlags.WidthFixed, 120f * ImGuiHelpers.GlobalScale);
                                ImGui.TableSetupColumn("Level", ImGuiTableColumnFlags.WidthFixed, 60f * ImGuiHelpers.GlobalScale);
                                ImGui.TableSetupColumn("Abbreviation", ImGuiTableColumnFlags.WidthFixed, 80f * ImGuiHelpers.GlobalScale);
                                ImGui.TableHeadersRow();

                                // Sort jobs within role by level (highest first), then by name
                                var sortedJobsInRole = roleGroup.OrderByDescending(j => j.Value).ThenBy(j => Utils.GetJobName(j.Key));

                                foreach (var job in sortedJobsInRole)
                                {
                                    ImGui.TableNextRow();
                                    
                                    // Icon column
                                    ImGui.TableSetColumnIndex(0);
                                    RenderJobIcon(job.Key, ImGuiHelpers.ScaledVector2(20f, 20f));

                                    // Job name column
                                    ImGui.TableSetColumnIndex(1);
                                    var jobName = Utils.GetJobName(job.Key);
                                    
                                    // Highlight main job
                                    if (job.Key == mainJobId)
                                    {
                                        using (var color = ThemeManager.PushColor(ImGuiCol.Text, ThemeManager.Colors.TextPrimary))
                                        {
                                            ImGui.Text($"★ {jobName}");
                                        }
                                    }
                                    else
                                    {
                                        ImGui.Text(jobName);
                                    }

                                    // Level column
                                    ImGui.TableSetColumnIndex(2);
                                    using (var color = ThemeManager.PushColor(ImGuiCol.Text, Utils.GetFFLogsLevelColor(job.Value)))
                                    {
                                        ImGui.Text(job.Value.ToString());
                                    }

                                    // Abbreviation column
                                    ImGui.TableSetColumnIndex(3);
                                    using (var color = ThemeManager.PushColor(ImGuiCol.Text, ThemeManager.Colors.TextMuted))
                                    {
                                        ImGui.Text(Utils.GetJobAbbreviation(job.Key));
                                    }
                                }

                                ImGui.EndTable();
                            }
                        }
                    }
                }

                ImGuiHelpers.ScaledDummy(10f);

                // Job Statistics
                if (ImGui.CollapsingHeader("Job Statistics"))
                {
                    var totalJobs = jobLevels.Count;
                    var maxLevel = jobLevels.Values.Max();
                    var averageLevel = jobLevels.Values.Select(v => (double)v).Average();
                    var jobsAt100Plus = jobLevels.Values.Count(l => l >= 100);

                    DrawInfoRow("Total Jobs", totalJobs.ToString());
                    DrawInfoRow("Highest Level", maxLevel.ToString());
                    DrawInfoRow("Average Level", $"{averageLevel:F1}");
                    DrawInfoRow("Jobs at Lv.100+", jobsAt100Plus.ToString());
                }
            }
            else
            {
                using (var color = ThemeManager.PushColor(ImGuiCol.Text, ThemeManager.Colors.TextMuted))
                {
                    ImGui.Text("No job data available yet.");
                    ImGui.Text("Job data will be collected when this player's Lodestone profile is refreshed.");
                }

                ImGuiHelpers.ScaledDummy(10f);

                if (ImGui.Button("Refresh Lodestone Data"))
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await PersistenceContext.RefreshPlayerImmediately(_contentId);
                        }
                        catch (Exception ex)
                        {
                            Plugin.Log.Error($"Failed to refresh player data: {ex}");
                        }
                    });
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Fetch job data from Lodestone immediately");
                }
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
            ImGui.Image(new ImTextureID(avatarHandle), avatarSize);
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

    private void RenderJobIcon(byte jobId, Vector2 iconSize)
    {
        var jobIcon = Utils.GetJobIcon(jobId);
        if (jobIcon != null)
        {
            var wrap = jobIcon.GetWrapOrEmpty();
            // Display the actual job icon
            ImGui.Image(wrap.Handle, iconSize);
        }
        else
        {
            // Fall back to placeholder button with job abbreviation
            var abbreviation = Utils.GetJobAbbreviation(jobId);
            ImGui.Button(abbreviation, iconSize);
        }
        
        // Add tooltip with full job name
        if (ImGui.IsItemHovered())
        {
            var jobName = Utils.GetJobName(jobId);
            ImGui.SetTooltip(jobName);
        }
    }

    private void RenderMinionIcon(string? iconUrl, Vector2 iconSize)
    {
        if (string.IsNullOrEmpty(iconUrl))
        {
            // Enhanced placeholder for missing URL
            var drawList = ImGui.GetWindowDrawList();
            var cursorPos = ImGui.GetCursorScreenPos();
            var rectMin = cursorPos;
            var rectMax = new Vector2(cursorPos.X + iconSize.X, cursorPos.Y + iconSize.Y);
            
            // Draw placeholder background with subtle gradient
            drawList.AddRectFilled(rectMin, rectMax, ImGui.ColorConvertFloat4ToU32(ThemeManager.Colors.Surface));
            drawList.AddRect(rectMin, rectMax, ImGui.ColorConvertFloat4ToU32(ThemeManager.Colors.Border));
            
            // Draw minion placeholder icon
            var textSize = ImGui.CalcTextSize("🐾");
            var textPos = new Vector2(
                cursorPos.X + (iconSize.X - textSize.X) / 2,
                cursorPos.Y + (iconSize.Y - textSize.Y) / 2
            );
            
            drawList.AddText(textPos, ImGui.ColorConvertFloat4ToU32(ThemeManager.Colors.TextMuted), "🐾");
            
            // Advance cursor
            ImGui.SetCursorScreenPos(new Vector2(cursorPos.X + iconSize.X, cursorPos.Y));
            return;
        }

        var iconHandle = Plugin.MinionCacheManager.GetMinionIconHandle(iconUrl);
        if (iconHandle != 0)
        {
            // Display the actual minion icon with subtle border
            var cursorPos = ImGui.GetCursorScreenPos();
            ImGui.Image(new ImTextureID(iconHandle), iconSize);
            
            // Add subtle border around loaded icons
            var drawList = ImGui.GetWindowDrawList();
            var rectMin = cursorPos;
            var rectMax = new Vector2(cursorPos.X + iconSize.X, cursorPos.Y + iconSize.Y);
            drawList.AddRect(rectMin, rectMax, ImGui.ColorConvertFloat4ToU32(ThemeManager.Colors.Border * 0.3f));
        }
        else
        {
            // Enhanced loading indicator
            var drawList = ImGui.GetWindowDrawList();
            var cursorPos = ImGui.GetCursorScreenPos();
            var rectMin = cursorPos;
            var rectMax = new Vector2(cursorPos.X + iconSize.X, cursorPos.Y + iconSize.Y);
            
            // Animated loading background
            var time = (float)ImGui.GetTime();
            var alpha = (Math.Sin(time * 2) + 1) / 4 + 0.1f; // Gentle pulsing
            var loadingColor = ThemeManager.Colors.Info * new Vector4(1, 1, 1, (float)alpha);
            
            drawList.AddRectFilled(rectMin, rectMax, ImGui.ColorConvertFloat4ToU32(loadingColor));
            drawList.AddRect(rectMin, rectMax, ImGui.ColorConvertFloat4ToU32(ThemeManager.Colors.Border));
            
            // Loading spinner or hourglass
            var loadingText = time % 2 < 1 ? "⏳" : "⌛";
            var textSize = ImGui.CalcTextSize(loadingText);
            var textPos = new Vector2(
                cursorPos.X + (iconSize.X - textSize.X) / 2,
                cursorPos.Y + (iconSize.Y - textSize.Y) / 2
            );
            
            drawList.AddText(textPos, ImGui.ColorConvertFloat4ToU32(ThemeManager.Colors.TextPrimary), loadingText);
            
            // Advance cursor
            ImGui.SetCursorScreenPos(new Vector2(cursorPos.X + iconSize.X, cursorPos.Y));
        }
        
        // Add hover tooltip
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(iconHandle != 0 ? "Minion Icon" : "Loading icon...");
        }
    }

    private Vector4 GetAcquisitionMethodColor(string acquisitionMethod)
    {
        return acquisitionMethod switch
        {
            "Quest Reward" => ThemeManager.Colors.Success,
            "Achievement" => new Vector4(1.0f, 0.84f, 0.0f, 1.0f), // Gold
            "Dungeon Drop" => new Vector4(0.5f, 0.8f, 1.0f, 1.0f), // Light blue
            "Raid Drop" => new Vector4(1.0f, 0.5f, 0.0f, 1.0f), // Orange
            "Market Board" => new Vector4(0.8f, 0.8f, 0.8f, 1.0f), // Silver
            "Market Board / Quest" => new Vector4(0.8f, 0.8f, 0.8f, 1.0f), // Silver
            "PvP Reward" => new Vector4(1.0f, 0.2f, 0.2f, 1.0f), // Red
            "PvP / Event" => new Vector4(1.0f, 0.2f, 0.2f, 1.0f), // Red
            "Seasonal Event" => new Vector4(1.0f, 0.4f, 0.8f, 1.0f), // Pink
            "Gold Saucer" => new Vector4(1.0f, 0.84f, 0.0f, 1.0f), // Gold
            "Retainer Venture" => new Vector4(0.6f, 0.4f, 0.8f, 1.0f), // Purple
            "Deep Dungeon" => new Vector4(0.4f, 0.2f, 0.6f, 1.0f), // Dark purple
            "Special Quest" => new Vector4(0.2f, 1.0f, 0.6f, 1.0f), // Mint green
            "Treasure Hunt" => new Vector4(1.0f, 0.6f, 0.2f, 1.0f), // Amber
            "Pre-order Bonus" => new Vector4(0.8f, 0.2f, 1.0f, 1.0f), // Magenta
            "Check Lodestone" => new Vector4(1.0f, 0.5f, 0.0f, 1.0f), // Orange
            _ => ThemeManager.Colors.TextMuted // Unknown or other
        };
    }
    
    private string GetAcquisitionTooltip(string method)
    {
        return method switch
        {
            "Quest Reward" => "Obtained by completing specific quests",
            "Dungeon Drop" => "Random drop from dungeon bosses",
            "Achievement" => "Earned through specific achievements",
            "Market Board" => "Can be purchased from other players",
            "Market Board / Quest" => "Available through market board or quest rewards",
            "Raid Drop" => "Rare drop from raid encounters",
            "PvP Reward" => "Earned through PvP activities",
            "PvP / Event" => "Available through PvP or seasonal events",
            "Seasonal Event" => "Limited-time seasonal event reward",
            "Retainer Venture" => "Found through retainer ventures",
            "Gold Saucer" => "Purchased with MGP at the Gold Saucer",
            "Deep Dungeon" => "Reward from Palace of the Dead or Heaven-on-High",
            "Special Quest" => "Obtained through special questlines",
            "Treasure Hunt" => "Found in treasure coffers",
            "Pre-order Bonus" => "Exclusive pre-order or collector's edition bonus",
            "Check Lodestone" => "Name failed to load - check Lodestone for details",
            _ => "Acquisition method unknown"
        };
    }

    /// <summary>
    /// Gets a display-friendly name for a minion, attempting to resolve actual names from icon URLs when possible
    /// </summary>
    private string GetMinionDisplayName(MinionInfo minion)
    {
        var name = minion.Name ?? "Unknown Minion";
        
        // If we already have a good name, use it
        if (!string.IsNullOrEmpty(name) && 
            !name.StartsWith("Minion #") && 
            !name.Contains(".png") &&
            name != "Unknown Minion")
        {
            return name;
        }
        
        // Try to derive a name from the icon URL
        if (!string.IsNullOrEmpty(minion.IconUrl))
        {
            // Extract filename from URL and try to match common patterns
            try
            {
                var uri = new Uri(minion.IconUrl);
                var filename = System.IO.Path.GetFileNameWithoutExtension(uri.AbsolutePath);
                
                // Common FFXIV minion icon patterns - map icon IDs to names
                var resolvedName = TryResolveMinionNameFromIconId(filename);
                if (!string.IsNullOrEmpty(resolvedName))
                {
                    return resolvedName;
                }
            }
            catch
            {
                // If URL parsing fails, continue with fallback
            }
        }
        
        // Return a better placeholder
        return "[Unknown Minion]";
    }
    
    /// <summary>
    /// Attempts to resolve minion names from icon IDs using known patterns
    /// This is a simplified mapping - a full implementation would use game data sheets
    /// </summary>
    private string? TryResolveMinionNameFromIconId(string iconId)
    {
        // Try to parse icon ID as a number and look it up in game data
        try
        {
            if (uint.TryParse(iconId, out var iconIdNum))
            {
                // Try to find the minion by icon ID using Lumina game data
                var companionSheet = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Companion>();
                if (companionSheet != null)
                {
                    foreach (var companion in companionSheet)
                    {
                        if (companion.Icon == iconIdNum && !string.IsNullOrEmpty(companion.Singular.ToString()))
                        {
                            return companion.Singular.ToString();
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Debug($"Failed to resolve minion name from icon ID {iconId}: {ex.Message}");
        }
        
        return null;
    }
}