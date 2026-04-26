using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Memoria.GUI.Modern.Components;
using Memoria.Handlers;
using Memoria.Utilities;

namespace Memoria.GUI.Modern.Views;

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

    // Per-tab list caches — refreshed at most once per RefreshInterval to avoid rebuilding
    // LINQ queries on every frame (~60fps) when a tab is visible.
    private IReadOnlyList<PlayerListItem>? _cachedRecent;
    private DateTime _cachedRecentAt;

    private IReadOnlyList<PlayerListItem>? _cachedFavorites;
    private DateTime _cachedFavoritesAt;

    private IReadOnlyList<PlayerListItem>? _cachedNotes;
    private DateTime _cachedNotesAt;

    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMilliseconds(500);

    // Web account link state. Tracks the in-flight generate request and the resulting code
    // until it expires; user re-clicks "Generate" to mint a new one once it lapses.
    private enum LinkState { Idle, Generating, Display, Error }
    private LinkState _linkState = LinkState.Idle;
    private string _linkCode = string.Empty;
    private DateTime _linkExpiresAt;
    private string _linkError = string.Empty;

    public SlimMainWindow(MicroCardWindow microCardWindow)
        : base("Memoria##Main", ImGuiWindowFlags.NoCollapse)
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
        ImGui.TextUnformatted("Memoria");
        ImGui.SameLine();
        var available = ImGui.GetContentRegionAvail().X;
        var btnText = "Open Memoria on the web →";
        var btnWidth = ImGui.CalcTextSize(btnText).X + ImGui.GetStyle().FramePadding.X * 2;
        ImGui.SameLine(0, available - btnWidth);
        if (ImGui.Button(btnText) && !string.IsNullOrWhiteSpace(config.WebBaseUrl))
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

    // ── List cache helpers ──────────────────────────────────────────────────────────────────

    private IReadOnlyList<PlayerListItem> GetRecentCached()
    {
        if (_cachedRecent is null || DateTime.UtcNow - _cachedRecentAt > RefreshInterval)
        {
            _cachedRecent = PlayerListProvider.GetRecent(PersistenceContext._playerCache, limit: 50);
            _cachedRecentAt = DateTime.UtcNow;
        }
        return _cachedRecent;
    }

    private IReadOnlyList<PlayerListItem> GetFavoritesCached()
    {
        if (_cachedFavorites is null || DateTime.UtcNow - _cachedFavoritesAt > RefreshInterval)
        {
            var favSet = new HashSet<long>(Plugin.Instance.Configuration.FavoritedPlayer.Keys);
            _cachedFavorites = PlayerListProvider.GetFavorites(PersistenceContext._playerCache, favSet);
            _cachedFavoritesAt = DateTime.UtcNow;
        }
        return _cachedFavorites;
    }

    private IReadOnlyList<PlayerListItem> GetNotesCached()
    {
        if (_cachedNotes is null || DateTime.UtcNow - _cachedNotesAt > RefreshInterval)
        {
            var withNotes = new HashSet<long>(
                Plugin.Instance.Configuration.FavoritedPlayer
                    .Where(kvp => !string.IsNullOrWhiteSpace(kvp.Value.Note))
                    .Select(kvp => kvp.Key));
            _cachedNotes = PlayerListProvider.GetFavorites(PersistenceContext._playerCache, withNotes);
            _cachedNotesAt = DateTime.UtcNow;
        }
        return _cachedNotes;
    }

    /// <summary>
    /// Expires all three list caches so the next frame re-builds them. Call after any mutation
    /// that should be immediately visible (favorite toggle, note edit).
    /// </summary>
    private void InvalidateListCaches()
    {
        _cachedRecentAt = DateTime.MinValue;
        _cachedFavoritesAt = DateTime.MinValue;
        _cachedNotesAt = DateTime.MinValue;
    }

    // ── Tab draw methods ────────────────────────────────────────────────────────────────────

    private void DrawRecent()
    {
        var items = GetRecentCached();
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
                InvalidateListCaches();
            });

            if (openClicked)
                _microCardWindow.OpenFor(item.ContentId);
        }
    }

    private void DrawFavorites()
    {
        var items = GetFavoritesCached();
        if (items.Count == 0)
        {
            ImGui.TextDisabled("No favorites yet. Open a player and click \"Add to favorites.\"");
            return;
        }

        DrawList(items);
    }

    private void DrawNotes()
    {
        var config = Plugin.Instance.Configuration;
        var items = GetNotesCached();
        if (items.Count == 0)
        {
            ImGui.TextDisabled("No player notes yet. Open a favorite and edit its note.");
            return;
        }

        foreach (var item in items)
        {
            var favKey = (long)item.ContentId;
            if (!config.FavoritedPlayer.TryGetValue(favKey, out var entry)) continue;

            ImGui.PushID($"note-{item.ContentId}");
            ImGui.TextUnformatted(item.Name);
            ImGui.SameLine();
            var worldName = item.HomeWorldId is { } wid && wid != 0 ? Utils.GetWorldName(wid) : "—";
            ImGui.TextDisabled($"({worldName})");
            var note = entry.Note ?? string.Empty;
            if (ImGui.InputTextMultiline("##note", ref note, 1000, new Vector2(-1, 60)))
            {
                entry.Note = note;
                config.Save();
                InvalidateListCaches();
            }
            ImGui.Separator();
            ImGui.PopID();
        }
    }
    private void DrawSearch()
    {
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##search", "Search by name…", ref _searchQuery, 100);

        var items = PlayerListProvider.Search(PersistenceContext._playerCache, _searchQuery);
        if (items.Count == 0 && !string.IsNullOrWhiteSpace(_searchQuery))
        {
            ImGui.TextDisabled("No matches in your local cache.");
            ImGui.TextDisabled("Tip: full search across the network is on the website.");
            var webBase = Plugin.Instance.Configuration.WebBaseUrl;
            if (ImGui.Button("Search on the web →") && !string.IsNullOrWhiteSpace(webBase))
            {
                // Land on the web search page; query parameter is web app's responsibility.
                var url = WebUrls.LandingUrl(webBase) + "/search?q=" +
                    System.Uri.EscapeDataString(_searchQuery.Trim());
                Dalamud.Utility.Util.OpenLink(url);
            }
            return;
        }

        DrawList(items);
    }
    private void DrawSettings()
    {
        var config = Plugin.Instance.Configuration;
        var changed = false;

        DrawUploadStatus();
        ImGui.Spacing();

        if (ImGui.Button("Browse my contributions on the web →") && !string.IsNullOrWhiteSpace(config.WebBaseUrl))
        {
            Dalamud.Utility.Util.OpenLink(WebUrls.MeUrl(config.WebBaseUrl));
        }

        ImGui.Spacing();
        DrawWebAccountLink();

        ImGui.Spacing();
        ImGui.TextDisabled("Privacy");

        var optOutPopups = config.OptOutInGamePopups;
        if (ImGui.Checkbox("Suppress in-game pop-ups (right-click context menu still works)", ref optOutPopups))
        {
            config.OptOutInGamePopups = optOutPopups;
            changed = true;
        }

        if (changed)
        {
            config.Save();
        }
    }

    private void DrawWebAccountLink()
    {
        var config = Plugin.Instance.Configuration;

        ImGui.TextDisabled("Web account link");
        ImGui.TextWrapped("Link this plugin install to your web account so claimed characters and privacy settings sync across the web and the plugin.");

        // Auto-clear the displayed code once it expires; user must regenerate.
        if (_linkState == LinkState.Display && DateTime.UtcNow >= _linkExpiresAt)
        {
            _linkState = LinkState.Idle;
            _linkCode = string.Empty;
        }

        switch (_linkState)
        {
            case LinkState.Idle:
                if (ImGui.Button("Generate web link code"))
                {
                    StartGenerateLinkCode();
                }
                break;

            case LinkState.Generating:
                ImGui.BeginDisabled();
                ImGui.Button("Generating…");
                ImGui.EndDisabled();
                break;

            case LinkState.Display:
                ImGui.PushFont(Dalamud.Interface.UiBuilder.MonoFont);
                ImGui.TextUnformatted(_linkCode);
                ImGui.PopFont();

                if (ImGui.Button("Copy code"))
                {
                    ImGui.SetClipboardText(_linkCode);
                }

                if (!string.IsNullOrWhiteSpace(config.WebBaseUrl))
                {
                    ImGui.SameLine();
                    if (ImGui.Button("Open the link page →"))
                    {
                        Dalamud.Utility.Util.OpenLink(WebUrls.LinkUrl(config.WebBaseUrl));
                    }
                }

                var remaining = _linkExpiresAt - DateTime.UtcNow;
                if (remaining > TimeSpan.Zero)
                {
                    ImGui.TextDisabled($"Expires in {(int)remaining.TotalMinutes}m {remaining.Seconds:00}s");
                }
                break;

            case LinkState.Error:
                ImGui.TextColored(new Vector4(1f, 0.4f, 0.4f, 1f), _linkError);
                if (ImGui.Button("Try again"))
                {
                    StartGenerateLinkCode();
                }
                break;
        }
    }

    private void StartGenerateLinkCode()
    {
        _linkState = LinkState.Generating;
        _linkError = string.Empty;

        // Fire-and-forget: API call runs off the framework thread; we update UI state from
        // the continuation. Errors are surfaced via _linkError without crashing the window.
        _ = System.Threading.Tasks.Task.Run(async () =>
        {
            try
            {
                var result = await Plugin.Instance.ApiClient.GenerateWebLinkCodeAsync();
                if (result.Success && result.Data is { } data && !string.IsNullOrWhiteSpace(data.Code))
                {
                    _linkCode = data.Code;
                    _linkExpiresAt = data.ExpiresAt;
                    _linkState = LinkState.Display;
                }
                else
                {
                    _linkError = result.Error ?? "Failed to generate link code.";
                    _linkState = LinkState.Error;
                }
            }
            catch (Exception ex)
            {
                _linkError = ex.Message;
                _linkState = LinkState.Error;
            }
        });
    }

    private static void DrawUploadStatus()
    {
        ImGui.TextDisabled("Upload status");
        var config = Plugin.Instance.Configuration;
        var queued = PersistenceContext._UploadPlayers.Count;
        var lastUpload = PersistenceContext.LastSuccessfulUploadAt;

        ImGui.TextUnformatted($"Lifetime contributions: {config.TotalContributions:N0}");

        if (queued == 0 && lastUpload is null)
        {
            ImGui.TextUnformatted("Nothing scanned yet. Walk past a player in-game.");
        }
        else
        {
            ImGui.TextUnformatted($"Queued: {queued}");
            if (lastUpload is { } last)
            {
                var agoText = Tools.ToTimeSinceString(Tools.ToUnixSecondsUtc(last));
                ImGui.TextUnformatted($"Last upload: {agoText}");
            }
            else
            {
                ImGui.TextUnformatted("Last upload: never");
            }
        }
    }
}
