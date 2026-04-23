using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;
using AlphaScope.GUI.Modern.Base;
using AlphaScope.Handlers;
using AlphaScope.Utilities;

namespace AlphaScope.GUI.Modern.Views;

/// <summary>
/// Small floating window opened from the right-click context menu (or a list-row click).
/// Shows just enough about a player to answer the in-game "who is this?" moment, then
/// funnels the user to the full profile on the web.
/// </summary>
internal sealed class MicroCardWindow : Window
{
    private ulong _targetContentId;

    public MicroCardWindow() : base("AlphaScope##MicroCard", ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoDocking)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(360, 220),
            MaximumSize = new Vector2(480, 320),
        };
        RespectCloseHotkey = true;
    }

    /// <summary>Opens the window for a specific player by ContentId.</summary>
    public void OpenFor(ulong contentId)
    {
        _targetContentId = contentId;
        if (PersistenceContext._playerCache.TryGetValue(contentId, out var p) && !string.IsNullOrEmpty(p.Name))
            WindowName = $"{p.Name}##MicroCard";
        else
            WindowName = "Player##MicroCard";
        IsOpen = true;
    }

    public override void Draw()
    {
        if (_targetContentId == 0)
        {
            ImGui.TextUnformatted("No player selected.");
            return;
        }

        if (!PersistenceContext._playerCache.TryGetValue(_targetContentId, out var player))
        {
            ImGui.TextUnformatted("Player not in local cache yet — try scrolling past them in-game first.");
            return;
        }

        var worldName = player.HomeWorldId is { } wid && wid != 0
            ? Utils.GetWorldName(wid)
            : "—";

        ImGui.TextUnformatted(player.Name);
        ImGui.TextUnformatted($"World: {worldName}");

        var lastSeenText = player.LastScannedAt is { } dt
            ? Tools.ToTimeSinceString((int)((DateTimeOffset)dt).ToUnixTimeSeconds())
            : "—";
        ImGui.TextUnformatted($"Last seen: {lastSeenText}");

        // Alts teaser (count only, names live on the web per Tier 3 privacy model)
        if (player.AccountId is { } accountId && accountId != 0)
        {
            var alts = PersistenceContext.GetAccountAltCharacters(accountId, _targetContentId);
            if (alts.Count > 0)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.788f, 0.663f, 0.380f, 1f)); // gold #c9a961
                ImGui.TextUnformatted($"→ {alts.Count} known alt{(alts.Count == 1 ? "" : "s")}");
                ImGui.PopStyleColor();
            }
        }

        ImGui.Separator();

        // Favorite toggle
        var config = Plugin.Instance.Configuration;
        var favKey = (long)_targetContentId;
        var isFavorite = config.FavoritedPlayer.ContainsKey(favKey);
        ImGui.PushStyleColor(ImGuiCol.Text, isFavorite ? ThemeManager.Colors.Error : ThemeManager.Colors.TextMuted);
        if (ImGuiComponents.IconButton(FontAwesomeIcon.Heart))
        {
            if (isFavorite)
            {
                config.FavoritedPlayer.TryRemove(favKey, out _);
            }
            else
            {
                config.FavoritedPlayer[favKey] = new Configuration.CachedFavoritedPlayer
                {
                    Name = player.Name,
                    AccountId = player.AccountId,
                    Note = string.Empty,
                };
            }
            config.Save();
        }
        ImGui.PopStyleColor();
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(isFavorite ? "Remove from favorites" : "Add to favorites");

        ImGui.SameLine();
        var worldUnknown = string.IsNullOrEmpty(worldName) || worldName == "—" || worldName == "Unknown";
        var canOpenProfile = !string.IsNullOrWhiteSpace(player.Name) && !worldUnknown;
        ImGui.BeginDisabled(!canOpenProfile);
        if (ImGui.Button("Open full profile in browser"))
        {
            var url = WebUrls.ProfileUrl(
                Plugin.Instance.Configuration.WebBaseUrl,
                player.Name,
                worldName);
            Dalamud.Utility.Util.OpenLink(url);
        }
        ImGui.EndDisabled();
    }
}
