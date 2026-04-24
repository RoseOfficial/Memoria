namespace MemoriaServer.Models.DTOs;

public record RecentPlayerResponse(
    IReadOnlyList<RecentPlayerItem> Items);

public record RecentPlayerItem(
    string Name,
    string WorldSlug,
    string WorldName,
    string? AvatarUrl,
    DateTime LastSeenAt);
