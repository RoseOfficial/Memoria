using System;
using System.ComponentModel.DataAnnotations;

namespace AlphaScope.Database;

/// <summary>
/// Entity representing a Final Fantasy XIV player character in the local database.
/// Stores basic character information including identity, job data, and account linking.
/// This is the local cache version - full character data is stored on the server.
/// </summary>
public class Player
{
    /// <summary>
    /// Unique Content ID for this character (FFXIV's internal character identifier).
    /// Serves as the primary key and is consistent across world transfers and name changes.
    /// </summary>
    [Key, Required]
    public ulong LocalContentId { get; set; }

    /// <summary>
    /// Current character name (up to 20 characters as per FFXIV naming rules).
    /// May change if player uses name change service, but ContentId remains constant.
    /// </summary>
    [MaxLength(20), Required]
    public string? Name { get; set; }

    /// <summary>
    /// FFXIV Account ID linking multiple characters to the same player account.
    /// Nullable because it may not be known immediately when a character is first encountered.
    /// Used to identify alt characters belonging to the same account.
    /// </summary>
    public ulong? AccountId { get; set; }

    /// <summary>
    /// ID of the character's home world (original world where character was created).
    /// This may differ from CurrentWorldId if the character has world transferred.
    /// </summary>
    public ushort? HomeWorldId { get; set; }

    /// <summary>
    /// ID of the world where the character is currently located.
    /// This may differ from HomeWorldId due to world visit or world transfer.
    /// </summary>
    public ushort? CurrentWorldId { get; set; }

    /// <summary>
    /// ID of the character's currently equipped job/class when last seen.
    /// Corresponds to FFXIV's internal job/class identification system.
    /// Nullable because job information may not be available in all scanning contexts.
    /// </summary>
    public byte? CurrentJobId { get; set; }

    /// <summary>
    /// Level of the character's currently equipped job/class when last seen.
    /// Range typically 1-100+ depending on current expansion level cap.
    /// Nullable because level information may not be available in all scanning contexts.
    /// </summary>
    public short? CurrentJobLevel { get; set; }

    /// <summary>
    /// URL to the character's Lodestone avatar image.
    /// Retrieved from FFXIV Lodestone and cached locally for display purposes.
    /// Nullable because not all characters may have Lodestone profiles or avatar data may not be fetched yet.
    /// </summary>
    [MaxLength(500)]
    public string? AvatarLink { get; set; }

    /// <summary>
    /// Timestamp of when this character's Lodestone data was last refreshed.
    /// Used by the background refresh service to determine refresh priority and track data freshness.
    /// Nullable because characters may not have been scanned yet by the refresh service.
    /// </summary>
    public DateTime? LastScannedAt { get; set; }

    /// <summary>
    /// JSON string containing complete job level data from Lodestone.
    /// Stores all job/class levels as a serialized dictionary for comprehensive job tracking.
    /// Example: {"1": 90, "2": 85, "3": 80} where keys are job IDs and values are levels.
    /// </summary>
    [MaxLength(2000)]
    public string? LodestoneJobData { get; set; }

    /// <summary>
    /// Main job/class ID from Lodestone profile (typically the highest level job).
    /// This represents the character's primary job as displayed on their Lodestone profile.
    /// </summary>
    public byte? MainJobId { get; set; }

    /// <summary>
    /// Level of the main job/class from Lodestone profile.
    /// Corresponds to the level of the MainJobId.
    /// </summary>
    public short? MainJobLevel { get; set; }

    /// <summary>
    /// Timestamp of when job data was last updated from Lodestone.
    /// Used to track freshness of job information separately from general profile scans.
    /// </summary>
    public DateTime? LastJobDataUpdate { get; set; }

    /// <summary>
    /// JSON string containing complete minion collection data from Lodestone.
    /// Stores all owned minions as a serialized array for comprehensive minion tracking.
    /// Example: [{"id": 1, "name": "Goobbue Sproutling", "acquired": "2024-01-01"}]
    /// </summary>
    [MaxLength(10000)]
    public string? LodestoneMinionsData { get; set; }

    /// <summary>
    /// Timestamp of when minion data was last updated from Lodestone.
    /// Used to track freshness of minion information separately from general profile scans.
    /// </summary>
    public DateTime? LastMinionsDataUpdate { get; set; }
}
