using System;
using System.Collections.Generic;
using System.Linq;
using AlphaScope.Handlers;

namespace AlphaScope.GUI.Modern.Components;

/// <summary>
/// Pure logic for filtering, sorting, and searching the in-memory player cache.
/// Kept separate from the windows that consume it so it stays unit-testable
/// (the Window classes aren't, because they wrap ImGui immediate-mode rendering).
/// </summary>
/// <remarks>
/// Callers pass the cache (or a snapshot of it) so tests can drive these methods
/// without touching live state.
/// </remarks>
internal static class PlayerListProvider
{
    /// <summary>Return up to <paramref name="limit"/> players, most-recently-scanned first.</summary>
    public static IReadOnlyList<PlayerListItem> GetRecent(
        IReadOnlyDictionary<ulong, PersistenceContext.CachedPlayer> cache,
        int limit)
    {
        return cache
            .OrderByDescending(kvp => kvp.Value.LastScannedAt ?? DateTime.MinValue)
            .Take(limit)
            .Select(ToItem)
            .ToList();
    }

    /// <summary>Return players whose ContentId is in <paramref name="favoriteContentIds"/>.</summary>
    public static IReadOnlyList<PlayerListItem> GetFavorites(
        IReadOnlyDictionary<ulong, PersistenceContext.CachedPlayer> cache,
        IReadOnlySet<long> favoriteContentIds)
    {
        return cache
            .Where(kvp => favoriteContentIds.Contains((long)kvp.Key))
            .OrderByDescending(kvp => kvp.Value.LastScannedAt ?? DateTime.MinValue)
            .Select(ToItem)
            .ToList();
    }

    /// <summary>Case-insensitive substring search by name. Empty/whitespace query returns empty.</summary>
    public static IReadOnlyList<PlayerListItem> Search(
        IReadOnlyDictionary<ulong, PersistenceContext.CachedPlayer> cache,
        string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return Array.Empty<PlayerListItem>();

        var trimmed = query.Trim();
        return cache
            .Where(kvp => kvp.Value.Name.Contains(trimmed, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(kvp => kvp.Value.LastScannedAt ?? DateTime.MinValue)
            .Select(ToItem)
            .ToList();
    }

    private static PlayerListItem ToItem(KeyValuePair<ulong, PersistenceContext.CachedPlayer> kvp) => new()
    {
        ContentId = kvp.Key,
        Name = kvp.Value.Name,
        HomeWorldId = kvp.Value.HomeWorldId,
        LastScannedAt = kvp.Value.LastScannedAt,
        AvatarLink = kvp.Value.AvatarLink,
    };
}

/// <summary>Compact view-model for one row in any of the player lists.</summary>
internal sealed record PlayerListItem
{
    public required ulong ContentId { get; init; }
    public required string Name { get; init; }
    public required ushort? HomeWorldId { get; init; }
    public required DateTime? LastScannedAt { get; init; }
    public string? AvatarLink { get; init; }
}
