namespace MemoriaServer.Models.DTOs;

public record PlayerProfileResponse(
    ProfileHeader Header,
    JobsData Jobs,
    CustomizationData? Customization,
    MountsData? Mounts,
    MinionsData? Minions,
    // Tier 2+ (null for anon viewers)
    LocationsData? Locations,
    IReadOnlyList<NameHistoryEntry>? NameHistory,
    IReadOnlyList<WorldHistoryEntry>? WorldHistory,
    IReadOnlyList<AltCharacter>? Alts,
    bool IsOwner);

public record ProfileHeader(
    long LocalContentId,
    string Name,
    string WorldSlug,
    string WorldName,
    string? AvatarUrl,
    byte? CurrentJobId,
    string? CurrentJobName,
    short? CurrentJobLevel,
    string? FreeCompanyTag,
    DateTime? LastSeenAt,
    string? LastSeenTerritory,
    DateTime? FirstScannedAt);

public record JobsData(IReadOnlyList<JobEntry> Jobs);
public record JobEntry(string Name, short Level);

public record CustomizationData(
    byte? BodyType, byte? GenderRace, byte? Height, byte? Face,
    byte? SkinColor, byte? Nose, byte? Jaw, byte? EyeShape);

public record MountsData(int Collected, int KnownTotal, IReadOnlyList<CollectibleIcon> Preview);
public record MinionsData(int Collected, int KnownTotal, IReadOnlyList<CollectibleIcon> Preview);
public record CollectibleIcon(int Id, string Name, string IconUrl);

public record LocationsData(IReadOnlyList<TerritoryEntry> Top);
public record TerritoryEntry(short TerritoryId, string TerritoryName, int VisitCount, DateTime LastVisitedAt);

public record NameHistoryEntry(string Name, DateTime ChangedAt);
public record WorldHistoryEntry(string WorldSlug, string WorldName, DateTime ChangedAt);
public record AltCharacter(string Name, string WorldSlug, string WorldName, long LocalContentId);
