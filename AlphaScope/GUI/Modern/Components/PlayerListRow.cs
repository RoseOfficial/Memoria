using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using AlphaScope.GUI.Modern.Base;
using AlphaScope.Handlers;

namespace AlphaScope.GUI.Modern.Components;

/// <summary>
/// Renders one compact row in any of the SlimMainWindow list tabs.
/// Pure render helper — no state. Caller passes in everything per call.
/// </summary>
internal static class PlayerListRow
{
    /// <summary>
    /// Draws a single row. Returns true if the user clicked the "open" button (caller
    /// should open MicroCardWindow for this player).
    /// </summary>
    internal static bool Draw(PlayerListItem item, bool isFavorite, Action<bool> onFavoriteToggled)
    {
        var openClicked = false;

        ImGui.BeginGroup();

        // Avatar slot — real texture when available, fallback rectangle while loading.
        var avatarSize = new Vector2(36, 36);
        nint textureHandle = 0;
        if (!string.IsNullOrEmpty(item.AvatarLink))
            textureHandle = Plugin.AvatarCacheManager.GetAvatarHandle(item.AvatarLink);

        if (textureHandle != 0)
        {
            var texId = NintToImTextureId(textureHandle);
            ImGui.Image(texId, avatarSize);
        }
        else
        {
            var pos = ImGui.GetCursorScreenPos();
            ImGui.GetWindowDrawList().AddRectFilled(pos, pos + avatarSize, ImGui.GetColorU32(ImGuiCol.FrameBg), 4f);
            ImGui.Dummy(avatarSize);
        }
        ImGui.SameLine();

        // Name + world stack
        var worldName = item.HomeWorldId is { } wid && wid != 0
            ? Utils.GetWorldName(wid)
            : "—";
        var lastSeenText = item.LastScannedAt is { } dt
            ? Tools.ToTimeSinceString((int)((DateTimeOffset)dt).ToUnixTimeSeconds())
            : "—";

        ImGui.BeginGroup();
        ImGui.TextUnformatted(item.Name);
        ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudGrey);
        ImGui.TextUnformatted($"{worldName} · {lastSeenText}");
        ImGui.PopStyleColor();
        ImGui.EndGroup();

        // Right-aligned actions
        var availableWidth = ImGui.GetContentRegionAvail().X;
        var actionsWidth = 70f;
        ImGui.SameLine(0, Math.Max(0, availableWidth - actionsWidth));

        // Use the full 64-bit ContentId as the ID. (int) truncation could collide
        // when two characters share the low 32 bits but differ in the upper bits
        // — FFXIV ContentIds are ~48-bit and the upper 16 vary by datacenter.
        ImGui.PushID(item.ContentId.ToString());
        ImGui.PushStyleColor(ImGuiCol.Text, isFavorite
            ? ThemeManager.Colors.Error
            : ThemeManager.Colors.TextMuted);
        if (ImGuiComponents.IconButton(FontAwesomeIcon.Heart))
        {
            onFavoriteToggled(!isFavorite);
        }
        ImGui.PopStyleColor();
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(isFavorite ? "Remove from favorites" : "Add to favorites");
        }
        ImGui.SameLine();
        if (ImGuiComponents.IconButton(FontAwesomeIcon.InfoCircle))
        {
            openClicked = true;
        }
        ImGui.PopID();

        ImGui.EndGroup();

        // Double-click anywhere on the row opens the profile (same as clicking the info icon).
        if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
        {
            openClicked = true;
        }

        return openClicked;
    }

    private static ImTextureID NintToImTextureId(nint handle)
    {
        return MemoryMarshal.Cast<nint, ImTextureID>(MemoryMarshal.CreateSpan(ref handle, 1))[0];
    }
}
