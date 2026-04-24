namespace AlphaScopeServer.Models.DTOs;

public record PlayerSearchResultResponse(IReadOnlyList<PlayerSearchItem> Items);

public record PlayerSearchItem(
    long LocalContentId,
    string Name,
    string WorldSlug,
    string WorldName,
    string? AvatarUrl);
