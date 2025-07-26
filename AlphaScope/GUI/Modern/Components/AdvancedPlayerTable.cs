using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ImGuiNET;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Components;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using OtterGui.Table;
using AlphaScope.GUI.Modern.Base;
using AlphaScope.Handlers;

namespace AlphaScope.GUI.Modern.Components;

/// <summary>
/// Advanced table component for displaying player data using OtterGui Table functionality.
/// Features sorting, filtering, column customization, and modern table design.
/// </summary>
public class AdvancedPlayerTable : BaseComponent
{
    private readonly Table<PlayerTableEntry> _table;
    private readonly List<PlayerTableEntry> _entries = new();
    private readonly List<PlayerTableEntry> _filteredEntries = new();
    private string _filterText = "";
    private bool _showFavoritesOnly = false;

    public AdvancedPlayerTable(string id) : base(id)
    {
        _table = new Table<PlayerTableEntry>($"PlayerTable_{id}", _filteredEntries,
            new NameColumn(),
            new ContentIdColumn(), 
            new AccountIdColumn(),
            new LastSeenColumn(),
            new ActionsColumn())
        {
            Flags = ImGuiTableFlags.Sortable | ImGuiTableFlags.Reorderable | ImGuiTableFlags.Hideable |
                   ImGuiTableFlags.BordersInner | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable
        };
    }

    protected override void OnRender()
    {
        // Refresh data if needed
        RefreshEntries();
        
        // Filter controls
        DrawFilterControls();
        
        // Table
        _table.Draw(ImGui.GetContentRegionAvail().Y - ImGuiHelpers.ScaledVector2(0f, 50f).Y);
        
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
                e.ContentId.ToString().Contains(lowerFilter) ||
                e.Player.AccountId?.ToString().Contains(lowerFilter) == true);
        }
        
        // Apply favorites filter
        if (_showFavoritesOnly)
        {
            filtered = filtered.Where(e => e.IsFavorited);
        }
        
        _filteredEntries.AddRange(filtered);
    }

    private class PlayerTableEntry
    {
        public ulong ContentId { get; set; }
        public PersistenceContext.CachedPlayer Player { get; set; } = null!;
        public DateTime LastSeen { get; set; }
        public bool IsFavorited { get; set; }
    }

    // Table column definitions using OtterGui
    private class NameColumn : ColumnString<PlayerTableEntry>
    {
        public NameColumn() : base() { Label = "Name"; Flags = ImGuiTableColumnFlags.DefaultSort; }
        public override string ToName(PlayerTableEntry item) => item.Player.Name;
    }

    private class ContentIdColumn : ColumnString<PlayerTableEntry>
    {
        public ContentIdColumn() : base() { Label = "Content ID"; Flags = ImGuiTableColumnFlags.None; }
        public override string ToName(PlayerTableEntry item) => item.ContentId.ToString();
    }

    private class AccountIdColumn : ColumnString<PlayerTableEntry>
    {
        public AccountIdColumn() : base() { Label = "Account ID"; Flags = ImGuiTableColumnFlags.None; }
        public override string ToName(PlayerTableEntry item) => item.Player.AccountId?.ToString() ?? "Unknown";
    }

    private class LastSeenColumn : Column<PlayerTableEntry>
    {
        public LastSeenColumn() : base() { Label = "Last Seen"; Flags = ImGuiTableColumnFlags.DefaultSort; }

        public override void DrawColumn(PlayerTableEntry item, int idx)
        {
            if (item.LastSeen == DateTime.MinValue)
            {
                using (var color = ThemeManager.PushColor(ImGuiCol.Text, ThemeManager.Colors.TextMuted))
                {
                    ImGui.Text("Never");
                }
            }
            else
            {
                var timeAgo = DateTime.Now - item.LastSeen;
                var timeText = FormatTimeAgo(timeAgo);
                ImGui.Text(timeText);
                
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip(item.LastSeen.ToString("yyyy-MM-dd HH:mm:ss"));
                }
            }
        }

        public override int Compare(PlayerTableEntry lhs, PlayerTableEntry rhs)
        {
            return DateTime.Compare(lhs.LastSeen, rhs.LastSeen);
        }

        private static string FormatTimeAgo(TimeSpan timeAgo)
        {
            if (timeAgo.TotalMinutes < 1) return "Just now";
            if (timeAgo.TotalHours < 1) return $"{(int)timeAgo.TotalMinutes}m ago";
            if (timeAgo.TotalDays < 1) return $"{(int)timeAgo.TotalHours}h ago";
            return $"{(int)timeAgo.TotalDays}d ago";
        }
    }

    private class ActionsColumn : Column<PlayerTableEntry>
    {
        public ActionsColumn() : base() { Label = "Actions"; Flags = ImGuiTableColumnFlags.NoSort | ImGuiTableColumnFlags.WidthFixed; }

        public override void DrawColumn(PlayerTableEntry item, int idx)
        {
            // Favorite button
            var heartIcon = item.IsFavorited ? FontAwesomeIcon.Heart : FontAwesomeIcon.Heart;
            var heartColor = item.IsFavorited ? ThemeManager.Colors.Error : ThemeManager.Colors.TextMuted;
            
            using (var color = ThemeManager.PushColor(ImGuiCol.Text, heartColor))
            {
                if (ImGuiComponents.IconButton($"fav_{item.ContentId}", heartIcon))
                {
                    ToggleFavorite(item);
                }
            }
            
            ImGui.SameLine();
            
            // Details button
            if (ImGuiComponents.IconButton($"details_{item.ContentId}", FontAwesomeIcon.InfoCircle))
            {
                OpenDetailsWindow(item.ContentId);
            }
        }

        private static void ToggleFavorite(PlayerTableEntry item)
        {
            var config = Plugin.Instance.Configuration;
            var longContentId = (long)item.ContentId;
            
            if (item.IsFavorited)
            {
                config.FavoritedPlayer.TryRemove(longContentId, out _);
                item.IsFavorited = false;
            }
            else
            {
                var favoriteData = new Configuration.CachedFavoritedPlayer
                {
                    Name = item.Player.Name,
                    AccountId = item.Player.AccountId ?? 0,
                    Note = ""
                };
                config.FavoritedPlayer[longContentId] = favoriteData;
                item.IsFavorited = true;
            }
            
            config.Save();
        }

        private static void OpenDetailsWindow(ulong contentId)
        {
            // TODO: Implement detailed player view in modern UI
            Plugin.Log.Info($"Opening details for player {contentId} - Feature coming soon in modern UI");
        }

        public override int Compare(PlayerTableEntry lhs, PlayerTableEntry rhs) => 0;
    }
}