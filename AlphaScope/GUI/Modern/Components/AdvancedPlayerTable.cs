using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Components;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using AlphaScope.GUI.Modern.Base;
using AlphaScope.Handlers;

namespace AlphaScope.GUI.Modern.Components;

/// <summary>
/// Advanced table component for displaying player data using direct ImGui table functionality.
/// Features sorting, filtering, and modern table design.
/// </summary>
public class AdvancedPlayerTable : BaseComponent
{
    private readonly List<PlayerTableEntry> _entries = new();
    private readonly List<PlayerTableEntry> _filteredEntries = new();
    private string _filterText = "";
    private bool _showFavoritesOnly = false;
    private int _sortColumn = 0;
    private ImGuiSortDirection _sortDirection = ImGuiSortDirection.Ascending;

    public AdvancedPlayerTable(string id) : base(id)
    {
    }

    protected override void OnRender()
    {
        // Refresh data if needed
        RefreshEntries();
        
        // Filter controls
        DrawFilterControls();
        
        // Table
        DrawTable();
        
        // Table info
        DrawTableInfo();
    }

    private void DrawFilterControls()
    {
        ImGui.SetNextItemWidth(200f * ImGuiHelpers.GlobalScale);
        if (ImGui.InputTextWithHint("##TableFilter", "Filter players...", ref _filterText, 256))
        {
            ApplyFilters();
        }
        
        ImGui.SameLine();
        if (ImGui.Checkbox("Favorites Only", ref _showFavoritesOnly))
        {
            ApplyFilters();
        }
        
        ImGui.SameLine();
        if (ImGui.Button("Clear Filters"))
        {
            _filterText = "";
            _showFavoritesOnly = false;
            ApplyFilters();
        }
        
        ImGui.Separator();
    }

    private void DrawTableInfo()
    {
        using (var color = ThemeManager.PushColor(ImGuiCol.Text, ThemeManager.Colors.TextSecondary))
        {
            ImGui.Text($"Showing {_filteredEntries.Count} of {_entries.Count} players");
        }
    }

    private void RefreshEntries()
    {
        _entries.Clear();
        
        foreach (var kvp in PersistenceContext._playerCache)
        {
            var entry = new PlayerTableEntry
            {
                ContentId = kvp.Key,
                Player = kvp.Value,
                LastSeen = GetLastSeenTime(kvp.Key),
                IsFavorited = Plugin.Instance.Configuration.FavoritedPlayer.ContainsKey((long)kvp.Key)
            };
            _entries.Add(entry);
        }
        
        ApplyFilters();
    }

    private DateTime GetLastSeenTime(ulong contentId)
    {
        if (PersistenceContext._recentlyScannedPlayers.TryGetValue(contentId, out var recentEntry))
        {
            return DateTimeOffset.FromUnixTimeSeconds(recentEntry.ScannedAt).DateTime;
        }
        return DateTime.MinValue;
    }

    private void ApplyFilters()
    {
        _filteredEntries.Clear();
        
        var filtered = _entries.AsEnumerable();
        
        // Apply text filter
        if (!string.IsNullOrWhiteSpace(_filterText))
        {
            var lowerFilter = _filterText.ToLower();
            filtered = filtered.Where(e => 
                e.Player.Name.ToLower().Contains(lowerFilter) ||
                (e.Player.HomeWorldId.HasValue && Utils.GetWorldName(e.Player.HomeWorldId.Value).ToLower().Contains(lowerFilter)));
        }
        
        // Apply favorites filter
        if (_showFavoritesOnly)
        {
            filtered = filtered.Where(e => e.IsFavorited);
        }
        
        _filteredEntries.AddRange(filtered);
        
        // Apply sorting
        ApplySorting();
    }
    
    private void ApplySorting()
    {
        if (_filteredEntries.Count == 0) return;
        
        _filteredEntries.Sort((a, b) =>
        {
            int result = 0;
            switch (_sortColumn)
            {
                case 0: // Name
                    result = string.Compare(a.Player.Name, b.Player.Name, StringComparison.OrdinalIgnoreCase);
                    break;
                case 1: // Home World
                    var worldNameA = a.Player.HomeWorldId.HasValue ? Utils.GetWorldName(a.Player.HomeWorldId.Value) : "Unknown";
                    var worldNameB = b.Player.HomeWorldId.HasValue ? Utils.GetWorldName(b.Player.HomeWorldId.Value) : "Unknown";
                    result = string.Compare(worldNameA, worldNameB, StringComparison.OrdinalIgnoreCase);
                    break;
                case 2: // Last Seen
                    result = DateTime.Compare(a.LastSeen, b.LastSeen);
                    break;
            }
            
            return _sortDirection == ImGuiSortDirection.Descending ? -result : result;
        });
    }

    private void DrawTable()
    {
        var availableHeight = ImGui.GetContentRegionAvail().Y - ImGuiHelpers.ScaledVector2(0f, 50f).Y;
        
        if (ImGui.BeginTable($"PlayerTable_{Id}", 4, 
            ImGuiTableFlags.Sortable | ImGuiTableFlags.BordersInner | ImGuiTableFlags.RowBg | 
            ImGuiTableFlags.Resizable | ImGuiTableFlags.ScrollY, new Vector2(0, availableHeight)))
        {
            // Setup columns
            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.DefaultSort);
            ImGui.TableSetupColumn("Home World");
            ImGui.TableSetupColumn("Last Seen");
            ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.NoSort | ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupScrollFreeze(0, 1);
            ImGui.TableHeadersRow();
            
            // Handle sorting
            var sortSpecs = ImGui.TableGetSortSpecs();
            if (sortSpecs.SpecsDirty)
            {
                if (sortSpecs.SpecsCount > 0)
                {
                    _sortColumn = sortSpecs.Specs.ColumnIndex;
                    _sortDirection = sortSpecs.Specs.SortDirection;
                    ApplySorting();
                }
                sortSpecs.SpecsDirty = false;
            }
            
            // Draw rows
            for (int i = 0; i < _filteredEntries.Count; i++)
            {
                var entry = _filteredEntries[i];
                ImGui.TableNextRow();
                
                // Name column
                ImGui.TableSetColumnIndex(0);
                ImGui.Text(entry.Player.Name);
                
                // Home World column
                ImGui.TableSetColumnIndex(1);
                if (entry.Player.HomeWorldId.HasValue)
                {
                    ImGui.Text(Utils.GetWorldName(entry.Player.HomeWorldId.Value));
                }
                else
                {
                    using (var color = ThemeManager.PushColor(ImGuiCol.Text, ThemeManager.Colors.TextMuted))
                    {
                        ImGui.Text("Unknown");
                    }
                }
                
                // Last Seen column
                ImGui.TableSetColumnIndex(2);
                DrawLastSeenCell(entry);
                
                // Actions column
                ImGui.TableSetColumnIndex(3);
                DrawActionsCell(entry);
            }
            
            ImGui.EndTable();
        }
    }
    
    private void DrawLastSeenCell(PlayerTableEntry entry)
    {
        if (entry.LastSeen == DateTime.MinValue)
        {
            using (var color = ThemeManager.PushColor(ImGuiCol.Text, ThemeManager.Colors.TextMuted))
            {
                ImGui.Text("Never");
            }
        }
        else
        {
            var timeAgo = DateTime.Now - entry.LastSeen;
            var timeText = FormatTimeAgo(timeAgo);
            ImGui.Text(timeText);
            
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(entry.LastSeen.ToString("yyyy-MM-dd HH:mm:ss"));
            }
        }
    }
    
    private void DrawActionsCell(PlayerTableEntry entry)
    {
        // Favorite button
        var heartIcon = entry.IsFavorited ? FontAwesomeIcon.Heart : FontAwesomeIcon.Heart;
        var heartColor = entry.IsFavorited ? ThemeManager.Colors.Error : ThemeManager.Colors.TextMuted;
        
        using (var color = ThemeManager.PushColor(ImGuiCol.Text, heartColor))
        {
            if (ImGuiComponents.IconButton($"fav_{entry.ContentId}", heartIcon))
            {
                ToggleFavorite(entry);
            }
        }
        
        ImGui.SameLine();
        
        // Details button
        if (ImGuiComponents.IconButton($"details_{entry.ContentId}", FontAwesomeIcon.InfoCircle))
        {
            OpenDetailsWindow(entry.ContentId);
        }
    }
    
    private static string FormatTimeAgo(TimeSpan timeAgo)
    {
        if (timeAgo.TotalMinutes < 1) return "Just now";
        if (timeAgo.TotalHours < 1) return $"{(int)timeAgo.TotalMinutes}m ago";
        if (timeAgo.TotalDays < 1) return $"{(int)timeAgo.TotalHours}h ago";
        return $"{(int)timeAgo.TotalDays}d ago";
    }
    
    private static void ToggleFavorite(PlayerTableEntry entry)
    {
        var config = Plugin.Instance.Configuration;
        var longContentId = (long)entry.ContentId;
        
        if (entry.IsFavorited)
        {
            config.FavoritedPlayer.TryRemove(longContentId, out _);
            entry.IsFavorited = false;
        }
        else
        {
            var favoriteData = new Configuration.CachedFavoritedPlayer
            {
                Name = entry.Player.Name,
                AccountId = entry.Player.AccountId ?? 0,
                Note = ""
            };
            config.FavoritedPlayer[longContentId] = favoriteData;
            entry.IsFavorited = true;
        }
        
        config.Save();
    }

    private static void OpenDetailsWindow(ulong contentId)
    {
        try
        {
            if (PersistenceContext._playerCache.TryGetValue(contentId, out var cachedPlayer))
            {
                // Check if window already exists for this content ID
                var existingWindow = Plugin.Instance.ws.Windows
                    .OfType<GUI.Modern.Views.PlayerDetailsWindow>()
                    .FirstOrDefault(w => w.ContentId == contentId);
                
                if (existingWindow != null)
                {
                    // Reuse existing window
                    existingWindow.IsOpen = true;
                    existingWindow.BringToFront();
                }
                else
                {
                    // Create new window
                    var detailsWindow = new GUI.Modern.Views.PlayerDetailsWindow(contentId, cachedPlayer);
                    Plugin.Instance.ws.AddWindow(detailsWindow);
                    detailsWindow.IsOpen = true;
                }
            }
            else
            {
                Plugin.Log.Warning($"Player {contentId} not found in cache for details window");
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"Failed to open details window for player {contentId}: {ex}");
        }
    }

    private class PlayerTableEntry
    {
        public ulong ContentId { get; set; }
        public PersistenceContext.CachedPlayer Player { get; set; } = null!;
        public DateTime LastSeen { get; set; }
        public bool IsFavorited { get; set; }
    }
}