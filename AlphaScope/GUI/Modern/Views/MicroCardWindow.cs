using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;
using Lumina.Excel.Sheets;
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
            MinimumSize = new Vector2(420, 260),
            MaximumSize = new Vector2(540, 360),
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

        var lastSeenText = player.LastScannedAt is { } dt
            ? Tools.ToTimeSinceString(Tools.ToUnixSecondsUtc(dt))
            : "—";

        // Header: avatar on the left, identity stack on the right
        ImGui.BeginGroup();

        var avatarSize = new Vector2(96, 96);
        nint textureHandle = 0;
        if (!string.IsNullOrEmpty(player.AvatarLink))
            textureHandle = Plugin.AvatarCacheManager.GetAvatarHandle(player.AvatarLink);

        if (textureHandle != 0)
        {
            var texId = NintToImTextureId(textureHandle);
            ImGui.Image(texId, avatarSize);
        }
        else
        {
            var pos = ImGui.GetCursorScreenPos();
            ImGui.GetWindowDrawList().AddRectFilled(pos, pos + avatarSize, ImGui.GetColorU32(ImGuiCol.FrameBg), 6f);
            ImGui.Dummy(avatarSize);
        }

        ImGui.SameLine();
        ImGui.BeginGroup();

        ImGui.TextUnformatted(player.Name);

        // World line — include traveling indicator when current world differs from home
        string worldLine;
        if (player.CurrentWorldId is { } curWid && curWid != 0
            && player.HomeWorldId is { } homeWid && homeWid != 0
            && curWid != homeWid)
        {
            worldLine = $"{Utils.GetWorldName(homeWid)} (traveling on {Utils.GetWorldName(curWid)})";
        }
        else
        {
            worldLine = worldName;
        }
        ImGui.TextUnformatted($"World: {worldLine}");

        // Main job + level if known
        if (player.MainJobId is { } jobId && jobId != 0 && player.MainJobLevel is { } jobLevel)
        {
            var jobAbbr = ResolveJobAbbreviation(jobId);
            ImGui.TextUnformatted($"Job: {jobAbbr} {jobLevel}");
        }

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

        ImGui.EndGroup();
        ImGui.EndGroup();

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

    private static string ResolveJobAbbreviation(byte jobId)
    {
        if (jobId == 0) return "???";
        try
        {
            var sheet = Plugin.DataManager.GetExcelSheet<ClassJob>();
            if (sheet?.GetRow(jobId) is not { } row) return "???";
            return row.Abbreviation.ExtractText();
        }
        catch
        {
            return "???";
        }
    }

    private static ImTextureID NintToImTextureId(nint handle)
    {
        return MemoryMarshal.Cast<nint, ImTextureID>(MemoryMarshal.CreateSpan(ref handle, 1))[0];
    }
}
