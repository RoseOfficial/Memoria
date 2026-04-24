namespace MemoriaServer.Models.DTOs;

public record ContributionsResponse(
    int Lifetime,
    IReadOnlyList<RecentContribution> Recent);

public record RecentContribution(
    string PlayerName,
    string WorldSlug,
    string WorldName,
    DateTime ScannedAt);
