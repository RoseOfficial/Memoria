using System.ComponentModel.DataAnnotations;

namespace AlphaScope.Database;

/// <summary>
/// Entity representing a Final Fantasy XIV retainer (NPC merchant) in the local database.
/// Retainers are player-owned NPCs that sell items on the market board and perform ventures.
/// Tracks retainer identity, world location, and ownership relationships.
/// </summary>
public class Retainer
{
    /// <summary>
    /// Unique Content ID for this retainer (FFXIV's internal retainer identifier).
    /// Serves as the primary key and remains constant throughout the retainer's lifetime.
    /// </summary>
    [Key, Required]
    public ulong LocalContentId { get; set; }

    /// <summary>
    /// Retainer name (up to 24 characters as per FFXIV retainer naming rules).
    /// Can be changed by the owner, so this may not be constant over time.
    /// </summary>
    [MaxLength(24), Required]
    public string? Name { get; set; }

    /// <summary>
    /// World/Server ID where this retainer is located.
    /// Corresponds to FFXIV's internal world identification system.
    /// Required because retainers are tied to specific worlds.
    /// </summary>
    [Required]
    public ushort WorldId { get; set; }

    /// <summary>
    /// Content ID of the player who owns this retainer.
    /// Links to Player.LocalContentId for relationship tracking.
    /// Nullable because ownership may not be determined immediately when retainer is first discovered.
    /// </summary>
    public ulong? OwnerLocalContentId { get; set; }
}
