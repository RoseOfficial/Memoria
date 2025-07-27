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
    /// ID of the character's currently equipped job/class when last seen.
    /// Corresponds to FFXIV's internal job/class identification system.
    /// Nullable because job information may not be available in all scanning contexts.
    /// </summary>
    public byte? CurrentJobId { get; set; }

    /// <summary>
    /// Level of the character's currently equipped job/class when last seen.
    /// Range typically 1-90+ depending on current expansion level cap.
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
}
