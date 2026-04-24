using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AlphaScopeServer.Models.Entities;

public enum TakedownStatus
{
    Pending = 0,
    Resolved = 1,
    Rejected = 2,
}

public class TakedownRequest
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(64)]
    public string WorldSlug { get; set; } = string.Empty;

    [Required, MaxLength(64)]
    public string NameSlug { get; set; } = string.Empty;

    // Best-effort resolve at submit time — can be null if the player isn't known yet.
    public long? ResolvedPlayerLocalContentId { get; set; }

    [Required, MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;

    [Required, MaxLength(320), EmailAddress]
    public string ContactEmail { get; set; } = string.Empty;

    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

    [Required, MaxLength(64)]
    public string SubmitterIpHash { get; set; } = string.Empty;

    public TakedownStatus Status { get; set; } = TakedownStatus.Pending;
    public DateTime? ResolvedAt { get; set; }
    public int? ResolvedByUserId { get; set; }

    [MaxLength(2000)]
    public string? ResolutionNotes { get; set; }

    [ForeignKey(nameof(ResolvedPlayerLocalContentId))]
    public virtual Player? ResolvedPlayer { get; set; }

    [ForeignKey(nameof(ResolvedByUserId))]
    public virtual ApplicationUser? ResolvedByUser { get; set; }
}
