using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Components;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.Utility.Raii;
using AlphaScope.GUI.Modern.Base;
using AlphaScope.GUI.Modern.Components;
using AlphaScope.Handlers;
using AlphaScope.Properties;
using AlphaScope.GUI;

namespace AlphaScope.GUI.Modern.Views;

/// <summary>
/// Modern main window for AlphaScope with dockable panels, enhanced search, and component-based architecture.
/// Replaces the legacy MainWindow with a more flexible and user-friendly interface.
/// </summary>
public class ModernMainWindow : BaseModernWindow
{
    private readonly List<PlayerCard> _playerCards = [];
    private string _searchText = "";
    private int _selectedTab = 0;
    private bool _showAdvancedFilters = false;
    private bool _showDatabaseClearConfirm = true;
    
    // Advanced filter state
    private bool _favoritesOnly = false;
    private readonly HashSet<uint> _selectedWorlds = new();
    private readonly HashSet<string> _selectedJobs = new();
    
    // View mode state
    private ViewMode _viewMode = ViewMode.Cards;
    private AdvancedPlayerTable? _playerTable;
    
    // Tab definitions
    private readonly string[] _tabs = { "Recent Players", "Search", "Favorites", "Analytics", "Social Network", "Reports", "Settings" };
    
    protected override bool EnableDocking => false; // Disabled until docking API is available

    public enum ViewMode
    {
        Cards,
        Table
    }

    public ModernMainWindow() : base("ModernAlphaScope", "AlphaScope")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(900, 600),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        // Initialize theme
        ThemeManager.Initialize();
    }

    protected override void OnInitialize()
    {
        RefreshPlayerCards();
        _playerTable = new AdvancedPlayerTable("MainWindow");
        AddComponent(_playerTable);
    }

    protected override void OnDraw()
    {
        // Setup docking space
        SetupDockSpace();
        
        // Draw main content based on selected tab
        DrawMainContent();
    }

    private void SetupDockSpace()
    {
        // Docking disabled for now - using regular window layout
        // TODO: Re-enable when docking API is available in this ImGui version
    }

    private void DrawMainContent()
    {
        // Menu bar
        DrawMenuBar();
        
        // Create a child region for the main content area
        using (var child = ImRaii.Child("MainContentArea", new Vector2(0, 0), false))
        {
            if (child)
            {
                // Use columns for layout without docking
                ImGui.Columns(2, "MainLayout", true);
                ImGui.SetColumnWidth(0, 200f * ImGuiHelpers.GlobalScale);
                
                // Left column: Navigation (with scrolling)
                using (var navChild = ImRaii.Child("NavigationArea", new Vector2(0, 0), false))
                {
                    if (navChild)
                    {
                        DrawNavigationContent();
                    }
                }
                
                ImGui.NextColumn();
                
                // Right column: Main content
                using (var contentChild = ImRaii.Child("ContentArea", Vector2.Zero, false))
                {
                    if (contentChild)
                    {
                        DrawContentArea();
                    }
                }
                
                ImGui.Columns(1);
            }
        }
        
    }

    private void DrawMenuBar()
    {
        if (ImGui.BeginMenuBar())
        {
            // Search section
            ImGui.SetNextItemWidth(300f * ImGuiHelpers.GlobalScale);
            if (ImGui.InputTextWithHint("##GlobalSearch", "Search players, IDs, worlds...", ref _searchText, 256))
            {
                OnSearchChanged();
            }
            
            ImGui.SameLine();
            
            // Filter button
            var filterColor = _showAdvancedFilters ? ThemeManager.Colors.Primary : ThemeManager.Colors.TextSecondary;
            using (var color = ThemeManager.PushColor(ImGuiCol.Text, filterColor))
            {
                if (ImGuiComponents.IconButton("filters", FontAwesomeIcon.Filter))
                {
                    _showAdvancedFilters = !_showAdvancedFilters;
                }
            }
            
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Advanced Filters");
            }
            
            // Spacer
            ImGui.Separator();
            
            // View mode toggle
            var viewIcon = _viewMode == ViewMode.Cards ? FontAwesomeIcon.Th : FontAwesomeIcon.List;
            var viewTooltip = _viewMode == ViewMode.Cards ? "Switch to Table View" : "Switch to Card View";
            
            if (ImGuiComponents.IconButton("viewmode", viewIcon))
            {
                _viewMode = _viewMode == ViewMode.Cards ? ViewMode.Table : ViewMode.Cards;
            }
            
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(viewTooltip);
            }
            
            // Settings button - show inline settings in modern UI
            if (ImGuiComponents.IconButton("settings", FontAwesomeIcon.Cog))
            {
                _selectedTab = Array.IndexOf(_tabs, "Settings");
            }
            
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Settings");
            }
            
            // Stats on the right
            ImGui.SetCursorPosX(ImGui.GetWindowWidth() - 200f * ImGuiHelpers.GlobalScale);
            using (var color = ThemeManager.PushColor(ImGuiCol.Text, ThemeManager.Colors.TextSecondary))
            {
                var playerCount = PersistenceContext._playerCache.Count;
                ImGui.Text($"Players: {playerCount:N0}");
            }
            
            ImGui.EndMenuBar();
        }
        
        // Advanced filters (if enabled)
        if (_showAdvancedFilters)
        {
            DrawAdvancedFilters();
        }
    }

    private void DrawNavigationContent()
    {
        // Breadcrumb navigation
        DrawBreadcrumbs();
        
        StyledSeparator();
        
        DrawHeader("Navigation");
        
        // Tab selection with icons and counts
        for (int i = 0; i < _tabs.Length; i++)
        {
            var isSelected = _selectedTab == i;
            var tabName = _tabs[i];
            var count = GetTabCount(i);
            
            // Add "Coming Soon" indicator for unimplemented tabs
            var isImplemented = i <= 2 || i == 6; // First 3 tabs and Settings are implemented
            if (!isImplemented)
            {
                tabName += " (Coming Soon)";
            }
            
            // Add count to tab name if available
            if (count > 0)
            {
                tabName += $" ({count})";
            }
            
            if (isSelected)
            {
                using var selectedColor = ThemeManager.PushColor(ImGuiCol.Button, ThemeManager.Colors.Primary);
                using var textColor = ThemeManager.PushColor(ImGuiCol.Text, ThemeManager.Colors.OnSurface);
                if (ImGui.Button($"{tabName}##{i}", new Vector2(-1, ImGuiHelpers.ScaledVector2(0f, 30f).Y)))
                {
                    _selectedTab = i;
                }
            }
            else
            {
                if (ImGui.Button($"{tabName}##{i}", new Vector2(-1, ImGuiHelpers.ScaledVector2(0f, 30f).Y)))
                {
                    _selectedTab = i;
                }
            }
            
            if (!isImplemented && ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("This feature is planned for a future update");
            }
        }
        
        StyledSeparator();
        
        // Quick actions with enhanced functionality
        DrawHeader("Quick Actions");
        
        using (ImRaii.Disabled(false))
        {
            if (ImGui.Button("Refresh Data", new Vector2(-1, 0)))
            {
                RefreshPlayerCards();
                ShowNotification("Data refreshed successfully!", NotificationType.Success);
            }
            
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Refresh player cache and recent players list");
            }
        }
        
        if (ImGui.Button("Export Data", new Vector2(-1, 0)))
        {
            ShowComingSoon("Data Export - JSON/CSV export coming soon!");
        }
        
        if (ImGui.Button("Import Data", new Vector2(-1, 0)))
        {
            ShowComingSoon("Data Import - Backup restore coming soon!");
        }
        
        StyledSeparator();
        
        // View options
        DrawHeader("View Options");
        
        
        if (ImGui.Button("Theme", new Vector2(-1, 0)))
        {
            ShowComingSoon("Theme Selector - Dark/Light themes coming soon!");
        }
    }

    private void DrawContentArea()
    {
        switch (_selectedTab)
        {
            case 0: // Recent Players
                DrawRecentPlayersTab();
                break;
            case 1: // Search
                DrawSearchTab();
                break;
            case 2: // Favorites
                DrawFavoritesTab();
                break;
            case 3: // Analytics (placeholder)
                ShowComingSoon("Analytics Dashboard");
                break;
            case 4: // Social Network (placeholder)
                ShowComingSoon("Social Network Analysis");
                break;
            case 5: // Reports (placeholder)
                ShowComingSoon("Advanced Reports");
                break;
            case 6: // Settings
                DrawSettingsTab();
                break;
        }
    }


    private void DrawAdvancedFilters()
    {
        using var child = ImRaii.Child("AdvancedFilters", new Vector2(0, ImGuiHelpers.ScaledVector2(0f, 120f).Y), true);
        if (!child) return;
        
        DrawHeader("Advanced Filters", "Filter search results by various criteria");
        
        // Filter options
        ImGui.Columns(3, "FilterColumns", false);
        
        // World filters
        ImGui.Text("World Filter:");
        var worldCount = _selectedWorlds.Count;
        var worldText = worldCount == 0 ? "All Worlds" : $"{worldCount} Selected";
        
        if (ImGui.Button($"{worldText}##WorldFilter"))
        {
            ShowComingSoon("World Selector - Coming in next update!");
        }
        
        if (worldCount > 0)
        {
            ImGui.SameLine();
            if (ImGui.SmallButton("Clear##WorldClear"))
            {
                _selectedWorlds.Clear();
            }
        }
        
        ImGui.NextColumn();
        
        // Job filters  
        ImGui.Text("Job Filter:");
        var jobCount = _selectedJobs.Count;
        var jobText = jobCount == 0 ? "All Jobs" : $"{jobCount} Selected";
        
        if (ImGui.Button($"{jobText}##JobFilter"))
        {
            ShowComingSoon("Job Selector - Coming in next update!");
        }
        
        if (jobCount > 0)
        {
            ImGui.SameLine();
            if (ImGui.SmallButton("Clear##JobClear"))
            {
                _selectedJobs.Clear();
            }
        }
        
        ImGui.NextColumn();
        
        // Other filters
        ImGui.Text("Quick Filters:");
        ImGui.Checkbox("Favorites Only", ref _favoritesOnly);
        
        if (ImGui.Button("Clear All Filters"))
        {
            _selectedWorlds.Clear();
            _selectedJobs.Clear();
            _favoritesOnly = false;
        }
        
        ImGui.Columns(1);
    }

    private void DrawRecentPlayersTab()
    {
        DrawHeader("Recent Players", "Recently encountered players");
        
        // Clean up old entries
        PersistenceContext.CleanupOldRecentPlayers();
        
        var recentPlayers = PersistenceContext._recentlyScannedPlayers
            .OrderByDescending(kvp => kvp.Value.ScannedAt)
            .Take(50)
            .ToList();
        
        if (recentPlayers.Count == 0)
        {
            using (var color = ThemeManager.PushColor(ImGuiCol.Text, ThemeManager.Colors.TextMuted))
            {
                ImGui.Text("No recent players found. Players will appear here as you encounter them in-game.");
            }
            return;
        }
        
        // Display as cards
        DrawPlayerCards(recentPlayers.Select(kvp => (kvp.Key, kvp.Value.Player)).ToList());
    }

    private void DrawSearchTab()
    {
        DrawHeader("Search", "Search for players and characters");
        
        // Search input (duplicated from menu bar for convenience)
        ImGui.SetNextItemWidth(400f * ImGuiHelpers.GlobalScale);
        if (ImGui.InputTextWithHint("##SearchInput", "Enter player name, ID, world...", ref _searchText, 256))
        {
            OnSearchChanged();
        }
        
        ImGui.SameLine();
        if (ImGui.Button("Search"))
        {
            OnSearchChanged();
        }
        
        StyledSeparator();
        
        if (string.IsNullOrWhiteSpace(_searchText))
        {
            using (var color = ThemeManager.PushColor(ImGuiCol.Text, ThemeManager.Colors.TextMuted))
            {
                ImGui.Text("Enter a search term to find players");
            }
            return;
        }
        
        // Search results with filters applied
        var searchResults = SearchPlayers(_searchText);
        var filteredResults = ApplyAdvancedFilters(searchResults);
        
        if (filteredResults.Any())
        {
            // Show filter summary if filters are active
            if (_favoritesOnly || _selectedWorlds.Any() || _selectedJobs.Any())
            {
                using (var color = ThemeManager.PushColor(ImGuiCol.Text, ThemeManager.Colors.TextSecondary))
                {
                    var totalResults = searchResults.Count;
                    var filteredCount = filteredResults.Count;
                    ImGui.Text($"Showing {filteredCount} of {totalResults} results");
                    
                    var filterDetails = new List<string>();
                    if (_favoritesOnly) filterDetails.Add("Favorites Only");
                    if (_selectedWorlds.Any()) filterDetails.Add($"{_selectedWorlds.Count} Worlds");
                    if (_selectedJobs.Any()) filterDetails.Add($"{_selectedJobs.Count} Jobs");
                    
                    if (filterDetails.Any())
                    {
                        ImGui.Text($"Filters: {string.Join(", ", filterDetails)}");
                    }
                }
                StyledSeparator();
            }
            
            DrawPlayerCards(filteredResults);
        }
        else
        {
            using (var color = ThemeManager.PushColor(ImGuiCol.Text, ThemeManager.Colors.TextMuted))
            {
                if (_favoritesOnly || _selectedWorlds.Any() || _selectedJobs.Any())
                {
                    ImGui.Text($"No results found for '{_searchText}' with current filters");
                    ImGui.Text("Try removing some filters to see more results");
                }
                else
                {
                    ImGui.Text($"No results found for '{_searchText}'");
                }
            }
        }
    }

    private void DrawFavoritesTab()
    {
        DrawHeader("Favorites", "Your favorited players");
        
        var config = Plugin.Instance.Configuration;
        var favoriteIds = config.FavoritedPlayer.Keys.ToList();
        
        if (favoriteIds.Count == 0)
        {
            using (var color = ThemeManager.PushColor(ImGuiCol.Text, ThemeManager.Colors.TextMuted))
            {
                ImGui.Text("No favorite players. Click the heart icon on player cards to add favorites.");
            }
            return;
        }
        
        var favoritePlayers = new List<(ulong, PersistenceContext.CachedPlayer)>();
        
        foreach (var favoriteId in favoriteIds)
        {
            var contentId = (ulong)favoriteId;
            if (PersistenceContext._playerCache.TryGetValue(contentId, out var cachedPlayer))
            {
                favoritePlayers.Add((contentId, cachedPlayer));
            }
        }
        
        if (favoritePlayers.Any())
        {
            DrawPlayerCards(favoritePlayers);
        }
        else
        {
            using (var color = ThemeManager.PushColor(ImGuiCol.Text, ThemeManager.Colors.TextMuted))
            {
                ImGui.Text("Favorite players not found in cache. Try refreshing or encountering them in-game.");
            }
        }
    }

    private void DrawSettingsTab()
    {
        DrawHeader("Settings", "AlphaScope configuration and preferences");
        
        var config = Plugin.Instance.Configuration;
        
        // Connection Settings
        if (ImGui.CollapsingHeader("Connection Settings", ImGuiTreeNodeFlags.DefaultOpen))
        {
            ImGui.Text("Server URL:");
            var baseUrl = config.BaseUrl;
            if (ImGui.InputText("##BaseUrl", ref baseUrl, 256))
            {
                config.BaseUrl = baseUrl;
                config.Save();
            }
            
            ImGui.Text("API Key:");
            var key = config.Key ?? "";
            if (ImGui.InputText("##ApiKey", ref key, 256, ImGuiInputTextFlags.Password))
            {
                config.Key = key;
                config.Save();
            }
            
            var isLoggedIn = config.LoggedIn;
            using (var color = ThemeManager.PushColor(ImGuiCol.Text, 
                isLoggedIn ? ThemeManager.Colors.Success : ThemeManager.Colors.Error))
            {
                ImGui.Text($"Status: {(isLoggedIn ? "Connected" : "Disconnected")}");
            }
            
            if (ImGui.Button("Test Connection"))
            {
                ShowComingSoon("Connection test coming soon!");
            }
        }
        
        // UI Settings
        if (ImGui.CollapsingHeader("Interface Settings"))
        {
            ShowComingSoon("UI customization options coming soon!");
        }
        
        // Data Settings
        if (ImGui.CollapsingHeader("Data Settings"))
        {
            // Cache statistics
            ImGui.Text("Cache Statistics:");
            var playerCount = PersistenceContext._playerCache.Count;
            var recentCount = PersistenceContext._recentlyScannedPlayers.Count;
            ImGui.Text($"Cached Players: {playerCount:N0}");
            ImGui.Text($"Recent Players: {recentCount:N0}");
            
            // Memory usage
            var memoryMB = GC.GetTotalMemory(false) / (1024 * 1024);
            ImGui.Text($"Memory Usage: {memoryMB:N0} MB");
            
            // Last sync info
            if (config.LastSyncedTime.HasValue)
            {
                var lastSync = DateTimeOffset.FromUnixTimeSeconds(config.LastSyncedTime.Value);
                var timeSince = Tools.ToTimeSinceString((int)config.LastSyncedTime.Value);
                ImGui.Text($"Last Sync: {timeSince}");
                
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip($"Full date: {lastSync.LocalDateTime}");
                }
            }
            else
            {
                ImGui.Text("Last Sync: Never");
            }
            
            ImGuiHelpers.ScaledDummy(5f);
            if (ImGui.Button("Clear Cache"))
            {
                PersistenceContext.ClearCache();
                ShowNotification("Cache cleared successfully!", NotificationType.Success);
            }
            
            ImGui.SameLine();
            using (var color = ThemeManager.PushColor(ImGuiCol.Button, ThemeManager.Colors.Error))
            {
                if (ImGui.Button("Clear Database"))
                {
                    ImGui.OpenPopup("ConfirmDatabaseClear");
                }
            }
            
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("WARNING: This will permanently delete all local player data!\nServer data remains safe.");
            }
            
            // Confirmation popup for database clear
            if (ImGui.BeginPopupModal("ConfirmDatabaseClear", ref _showDatabaseClearConfirm, ImGuiWindowFlags.AlwaysAutoResize))
            {
                ImGui.Text("Are you sure you want to clear the entire local database?");
                ImGui.Text("This will permanently delete ALL cached player data.");
                ImGui.Separator();
                
                using (var color = ThemeManager.PushColor(ImGuiCol.Text, ThemeManager.Colors.TextMuted))
                {
                    ImGui.Text("• All player names, avatars, and cache data will be lost");
                    ImGui.Text("• Data will be rebuilt as you encounter players in-game");
                    ImGui.Text("• Server data remains completely unaffected");
                    ImGui.Text("• This action cannot be undone");
                }
                
                ImGui.Separator();
                ImGuiHelpers.ScaledDummy(5f);
                
                using (var buttonColor = ThemeManager.PushColor(ImGuiCol.Button, ThemeManager.Colors.Error))
                {
                    if (ImGui.Button("Yes, Clear Database"))
                    {
                        try
                        {
                            PersistenceContext.ClearDatabase();
                            ShowNotification("Database cleared successfully!", NotificationType.Success);
                            ImGui.CloseCurrentPopup();
                        }
                        catch (Exception ex)
                        {
                            Plugin.Log.Error(ex, "Failed to clear database");
                            ShowNotification("Failed to clear database - check logs", NotificationType.Error);
                        }
                    }
                }
                
                ImGui.SameLine();
                if (ImGui.Button("Cancel"))
                {
                    ImGui.CloseCurrentPopup();
                }
                
                ImGui.EndPopup();
            }
        }
    }

    private void DrawPlayerCards(List<(ulong contentId, PersistenceContext.CachedPlayer player)> players)
    {
        if (_viewMode == ViewMode.Table && _playerTable != null)
        {
            // Update table data and render
            _playerTable.Render();
        }
        else
        {
            // Render card view with proper spacing and responsive layout
            var cardWidth = 320f * ImGuiHelpers.GlobalScale; // Updated to match PlayerCard size
            var cardHeight = 120f * ImGuiHelpers.GlobalScale;
            var spacing = 10f * ImGuiHelpers.GlobalScale; // Slightly larger spacing for better visual separation
            var availableWidth = ImGui.GetContentRegionAvail().X;
            var cardsPerRow = Math.Max(1, (int)((availableWidth + spacing) / (cardWidth + spacing)));
            
            for (int i = 0; i < players.Count; i++)
            {
                var (contentId, player) = players[i];
                
                // Calculate position for proper grid layout
                var row = i / cardsPerRow;
                var col = i % cardsPerRow;
                
                if (col > 0)
                {
                    ImGui.SameLine();
                }
                
                var card = new PlayerCard($"card_{contentId}_{i}", contentId, player);
                card.Render();
                
                // Add spacing between cards in the same row
                if (col < cardsPerRow - 1 && i < players.Count - 1)
                {
                    ImGui.SameLine(0, spacing);
                }
            }
        }
    }

    private List<(ulong, PersistenceContext.CachedPlayer)> SearchPlayers(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return [];
        
        var results = new List<(ulong contentId, PersistenceContext.CachedPlayer player, int relevanceScore)>();
        var lowerSearch = searchTerm.ToLower();
        
        foreach (var kvp in PersistenceContext._playerCache)
        {
            var contentId = kvp.Key;
            var cachedPlayer = kvp.Value;
            var playerNameLower = cachedPlayer.Name.ToLower();
            int score = 0;
            
            // Exact name match (highest priority)
            if (playerNameLower == lowerSearch)
            {
                score = 100;
            }
            // Name starts with search term (high priority)
            else if (playerNameLower.StartsWith(lowerSearch))
            {
                score = 80;
            }
            // Name contains search term (medium priority)
            else if (playerNameLower.Contains(lowerSearch))
            {
                score = 60;
            }
            // Exact content ID match
            else if (contentId.ToString() == searchTerm)
            {
                score = 90;
            }
            // Content ID contains search term
            else if (contentId.ToString().Contains(lowerSearch))
            {
                score = 50;
            }
            // Account ID exact match
            else if (cachedPlayer.AccountId?.ToString() == searchTerm)
            {
                score = 85;
            }
            // Account ID contains search term
            else if (cachedPlayer.AccountId?.ToString().Contains(lowerSearch) == true)
            {
                score = 40;
            }
            
            if (score > 0)
            {
                results.Add((contentId, cachedPlayer, score));
            }
        }
        
        // Sort by relevance score (descending) then by name
        return results
            .OrderByDescending(r => r.relevanceScore)
            .ThenBy(r => r.player.Name)
            .Take(100) // Limit results for performance
            .Select(r => (r.contentId, r.player))
            .ToList();
    }

    private List<(ulong, PersistenceContext.CachedPlayer)> ApplyAdvancedFilters(List<(ulong, PersistenceContext.CachedPlayer)> results)
    {
        if (!_favoritesOnly && !_selectedWorlds.Any() && !_selectedJobs.Any())
        {
            return results; // No filters active
        }
        
        var filteredResults = results.AsEnumerable();
        
        // Apply favorites filter
        if (_favoritesOnly)
        {
            var config = Plugin.Instance.Configuration;
            filteredResults = filteredResults.Where(r => config.FavoritedPlayer.ContainsKey((long)r.Item1));
        }
        
        // Apply world filter (placeholder - would need world data)
        if (_selectedWorlds.Any())
        {
            // TODO: Filter by world when world data is available
            // filteredResults = filteredResults.Where(r => _selectedWorlds.Contains(r.Item2.WorldId));
        }
        
        // Apply job filter (placeholder - would need job data)
        if (_selectedJobs.Any())
        {
            // TODO: Filter by job when job data is available
            // filteredResults = filteredResults.Where(r => _selectedJobs.Contains(r.Item2.JobName));
        }
        
        return filteredResults.ToList();
    }

    private void OnSearchChanged()
    {
        // Switch to search tab if not already there
        if (_selectedTab != 1)
        {
            _selectedTab = 1;
        }
    }

    private void RefreshPlayerCards()
    {
        _playerCards.Clear();
        // Player cards are now created on-demand during rendering
        // This method can be extended to trigger cache refresh if needed
    }
    
    private void StyledSeparator()
    {
        ImGui.Separator();
    }
    
    private void ShowComingSoon(string featureName)
    {
        using (var color = ThemeManager.PushColor(ImGuiCol.Text, ThemeManager.Colors.TextMuted))
        {
            ImGui.Text($"{featureName} is coming soon!");
        }
    }
    
    private void DrawBreadcrumbs()
    {
        using (var headerColor = ThemeManager.PushColor(ImGuiCol.Text, ThemeManager.Colors.TextSecondary))
        {
            ImGui.Text("AlphaScope");
        }
        
        ImGui.SameLine();
        ImGui.Text(">");
        ImGui.SameLine();
        
        using (var primaryColor = ThemeManager.PushColor(ImGuiCol.Text, ThemeManager.Colors.Primary))
        {
            ImGui.Text(_tabs[_selectedTab]);
        }
    }
    
    private FontAwesomeIcon GetTabIcon(int tabIndex)
    {
        return tabIndex switch
        {
            0 => FontAwesomeIcon.Clock,        // Recent Players
            1 => FontAwesomeIcon.Search,       // Search
            2 => FontAwesomeIcon.Heart,        // Favorites
            3 => FontAwesomeIcon.ChartBar,     // Analytics
            4 => FontAwesomeIcon.Users,        // Social Network
            5 => FontAwesomeIcon.FileAlt,      // Reports
            _ => FontAwesomeIcon.Question
        };
    }
    
    private int GetTabCount(int tabIndex)
    {
        return tabIndex switch
        {
            0 => PersistenceContext._recentlyScannedPlayers.Count,  // Recent Players count
            1 => 0,  // Search has no static count
            2 => Plugin.Instance.Configuration.FavoritedPlayer.Count,  // Favorites count
            3 => 0,  // Analytics placeholder
            4 => 0,  // Social Network placeholder
            5 => 0,  // Reports placeholder
            _ => 0
        };
    }
    
    public enum NotificationType
    {
        Info,
        Success,
        Warning,
        Error
    }
    
    private void ShowNotification(string message, NotificationType type)
    {
        // Simple notification system - could be enhanced with actual toast notifications
        Plugin.Log.Info($"[UI] {message}");
    }

    protected override void OnWindowClose()
    {
        foreach (var card in _playerCards)
        {
            card.Dispose();
        }
        _playerCards.Clear();
    }
}