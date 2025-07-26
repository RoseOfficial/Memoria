using System;
using System.Numerics;
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
    private string _avatarUrl = string.Empty;
    
    internal PersistenceContext.CachedPlayer Player => _cachedPlayer;
    public ulong ContentId => _contentId;

    internal PlayerCard(string id, ulong contentId, PersistenceContext.CachedPlayer cachedPlayer) 
        : base(id)
    {
        _contentId = contentId;
        _cachedPlayer = cachedPlayer ?? throw new ArgumentNullException(nameof(cachedPlayer));
        _isFavorited = Plugin.Instance.Configuration.FavoritedPlayer.ContainsKey((long)contentId);
        
        // Try to get avatar URL (placeholder for now)
        _avatarUrl = ""; // TODO: Get from avatar cache when implemented
    }

    protected override void OnRender()
    {
        var cardSize = ImGuiHelpers.ScaledVector2(300f, 120f);
        
        using var childId = ImRaii.PushId(_id);
        
        // Use ImGui's built-in layout system instead of manual positioning
        using (var child = ImRaii.Child($"card_{_id}", cardSize, true, ImGuiWindowFlags.NoScrollbar))
        {
            if (!child) return;
            
            // Left side: Avatar placeholder and basic info
            var avatarSize = ImGuiHelpers.ScaledVector2(64f, 64f);
            
            // Simple avatar placeholder using button
            ImGui.Button("👤", avatarSize);
            
            ImGui.SameLine();
            
            // Player info column
            ImGui.BeginGroup();
            
            // Player name
            using (var nameColor = ThemeManager.PushColor(ImGuiCol.Text, ThemeManager.Colors.TextPrimary))
            {
                ImGui.Text(_cachedPlayer.Name);
            }
            
            // Job information
            using (var jobColor = ThemeManager.PushColor(ImGuiCol.Text, ThemeManager.Colors.TextSecondary))
            {
                ImGui.Text("Unknown Job");
            }
            
            // Status
            using (var statusColor = ThemeManager.PushColor(ImGuiCol.Text, ThemeManager.Colors.TextMuted))
            {
                ImGui.Text("● Last seen: Unknown");
            }
            
            ImGui.EndGroup();
            
            // Right side: Action buttons
            ImGui.SameLine();
            
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
        // TODO: Implement detailed player view in modern UI
        Plugin.Log.Info($"Opening details for player {_contentId} - Feature coming soon in modern UI");
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
}