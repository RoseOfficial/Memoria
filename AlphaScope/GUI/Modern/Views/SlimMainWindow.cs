using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using AlphaScope.GUI.Modern.Components;
using AlphaScope.Handlers;
using AlphaScope.Utilities;

namespace AlphaScope.GUI.Modern.Views;

/// <summary>
/// Replacement main plugin window. Slim by design: 4 nav tabs + Settings, plus a
/// persistent CTA at the top driving users to the web app.
/// </summary>
internal sealed class SlimMainWindow : Window
{
    public enum Tab { Recent, Favorites, Notes, Search, Settings }

    private Tab _active = Tab.Recent;
    private string _searchQuery = string.Empty;

    private readonly MicroCardWindow _microCardWindow;

    public SlimMainWindow(MicroCardWindow microCardWindow)
        : base("AlphaScope##Main", ImGuiWindowFlags.NoCollapse)
    {
        _microCardWindow = microCardWindow;
        Size = new Vector2(720, 480);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    /// <summary>
    /// Open the window on the default tab (Recent). Use this for /alpha and OpenMainUi —
    /// not bare IsOpen = true, which would leave _active wherever it was last set.
    /// </summary>
    public void OpenDefault()
    {
        _active = Tab.Recent;
        IsOpen = true;
    }

    /// <summary>Programmatic entry point for "open at search tab with this query."</summary>
    public void OpenAtSearch(string prepopulatedQuery)
    {
        _active = Tab.Search;
        _searchQuery = prepopulatedQuery ?? string.Empty;
        IsOpen = true;
    }

    public override void Draw()
    {
        DrawTopBar();
        ImGui.Separator();
        DrawSplit();
    }

    private void DrawTopBar()
    {
        var config = Plugin.Instance.Configuration;
        ImGui.TextUnformatted("AlphaScope");
        ImGui.SameLine();
        var available = ImGui.GetContentRegionAvail().X;
        var btnText = "Open AlphaScope on the web →";
        var btnWidth = ImGui.CalcTextSize(btnText).X + ImGui.GetStyle().FramePadding.X * 2;
        ImGui.SameLine(0, available - btnWidth);
        if (ImGui.Button(btnText))
        {
            Dalamud.Utility.Util.OpenLink(WebUrls.LandingUrl(config.WebBaseUrl));
        }
    }

    private void DrawSplit()
    {
        if (ImGui.BeginChild("##nav", new Vector2(160, 0), true))
        {
            DrawNavItem(Tab.Recent, "Recent");
            DrawNavItem(Tab.Favorites, "Favorites");
            DrawNavItem(Tab.Notes, "Notes");
            DrawNavItem(Tab.Search, "Search");

            // Push Settings to the bottom
            var avail = ImGui.GetContentRegionAvail().Y;
            ImGui.Dummy(new Vector2(0, System.Math.Max(0, avail - ImGui.GetTextLineHeightWithSpacing())));
            DrawNavItem(Tab.Settings, "Settings");
        }
        ImGui.EndChild();

        ImGui.SameLine();

        if (ImGui.BeginChild("##body", new Vector2(0, 0), false))
        {
            switch (_active)
            {
                case Tab.Recent: DrawRecent(); break;
                case Tab.Favorites: DrawFavorites(); break;
                case Tab.Notes: DrawNotes(); break;
                case Tab.Search: DrawSearch(); break;
                case Tab.Settings: DrawSettings(); break;
            }
        }
        ImGui.EndChild();
    }

    private void DrawNavItem(Tab tab, string label)
    {
        var selected = _active == tab;
        if (ImGui.Selectable(label, selected))
        {
            _active = tab;
        }
    }

    private void DrawRecent()
    {
        var items = PlayerListProvider.GetRecent(PersistenceContext._playerCache, limit: 50);
        if (items.Count == 0)
        {
            ImGui.TextDisabled("No recent encounters yet. Walk past some players in-game.");
            return;
        }

        DrawList(items);
    }

    private void DrawList(IReadOnlyList<PlayerListItem> items)
    {
        var config = Plugin.Instance.Configuration;
        foreach (var item in items)
        {
            var favKey = (long)item.ContentId;
            var isFav = config.FavoritedPlayer.ContainsKey(favKey);
            var openClicked = PlayerListRow.Draw(item, isFav, newFav =>
            {
                if (newFav)
                {
                    ulong? accountId = null;
                    if (PersistenceContext._playerCache.TryGetValue(item.ContentId, out var cachedPlayer))
                        accountId = cachedPlayer.AccountId;
                    config.FavoritedPlayer[favKey] = new Configuration.CachedFavoritedPlayer
                    {
                        Name = item.Name,
                        AccountId = accountId,
                        Note = string.Empty,
                    };
                }
                else
                {
                    config.FavoritedPlayer.TryRemove(favKey, out _);
                }
                config.Save();
            });

            if (openClicked)
                _microCardWindow.OpenFor(item.ContentId);
        }
    }

    // Stubs for subsequent tasks
    private void DrawFavorites() => ImGui.TextDisabled("Favorites — implemented in Task 9.");
    private void DrawNotes() => ImGui.TextDisabled("Notes — implemented in Task 10.");
    private void DrawSearch() => ImGui.TextDisabled("Search — implemented in Task 11.");
    private void DrawSettings() => ImGui.TextDisabled("Settings — implemented in Task 12.");
}
