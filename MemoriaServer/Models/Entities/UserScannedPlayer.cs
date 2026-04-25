using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MemoriaServer.Models.Entities
{
    /// <summary>
    /// Tracks per-(user, player) most-recent-scan timestamps so the dashboard's
    /// "Recent contributions" list can show distinct players the user has scanned
    /// ordered by recency. One row per unique (UserId, PlayerLocalContentId);
    /// updated on every scan upload from the user's plugin.
    ///
    /// Cascade-deletes with the user — when a user is merged via link/redeem and
    /// the webUser row is removed, that webUser's scan attribution disappears with
    /// it. Webusers don't upload scans, so this is the right behavior.
    /// </summary>
    [Table("UserScannedPlayers")]
    public class UserScannedPlayer
    {
        [Required]
        public int UserId { get; set; }

        [Required]
        public long PlayerLocalContentId { get; set; }

        [Required]
        public DateTime LastScannedAt { get; set; }

        [ForeignKey(nameof(UserId))]
        public virtual ApplicationUser? User { get; set; }

        [ForeignKey(nameof(PlayerLocalContentId))]
        public virtual Player? Player { get; set; }
    }
}
