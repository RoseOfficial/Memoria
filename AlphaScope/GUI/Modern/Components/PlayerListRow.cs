using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;

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
    public static bool Draw(PlayerListItem item, bool isFavorite, Action<bool> onFavoriteToggled)
    {
        var openClicked = false;
        var rowId = $"##row-{item.ContentId}";

        ImGui.BeginGroup();

        // Avatar slot — placeholder for now; actual texture wiring lives in MicroCardWindow.
        // Reserves vertical space so all rows align.
        var avatarSize = new Vector2(36, 36);
        var pos = ImGui.GetCursorScreenPos();
        ImGui.GetWindowDrawList().AddRectFilled(pos, pos + avatarSize, ImGui.GetColorU32(ImGuiCol.FrameBg), 4f);
        ImGui.Dummy(avatarSize);
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

        ImGui.PushID(rowId);
        if (ImGuiComponents.IconButton(FontAwesomeIcon.Heart))
        {
            onFavoriteToggled(!isFavorite);
        }
        ImGui.SameLine();
        if (ImGuiComponents.IconButton(FontAwesomeIcon.InfoCircle))
        {
            openClicked = true;
        }
        ImGui.PopID();

        ImGui.EndGroup();

        return openClicked;
    }
}
