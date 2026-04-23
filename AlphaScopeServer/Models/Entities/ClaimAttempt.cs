using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AlphaScopeServer.Models.Entities
{
    /// <summary>
    /// A pending character-claim verification. One row per (user, player) pair; starting a
    /// new attempt for the same pair overwrites the existing row (see PlayersController).
    /// On successful verify, the row is deleted and the Player row's Claimed* columns are set.
    /// </summary>
    [Table("ClaimAttempts")]
    public class ClaimAttempt
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        public long PlayerLocalContentId { get; set; }

        [Required]
        [MaxLength(16)]
        public string Code { get; set; } = string.Empty;

        [Required]
        public DateTime ExpiresAt { get; set; }

        public int Attempts { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(UserId))]
        public virtual ApplicationUser User { get; set; } = null!;

        [ForeignKey(nameof(PlayerLocalContentId))]
        public virtual Player Player { get; set; } = null!;
    }
}
