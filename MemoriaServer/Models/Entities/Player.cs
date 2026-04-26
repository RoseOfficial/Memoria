using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MemoriaServer.Models.Entities
{
    [Table("Players")]
    public class Player
    {
        [Key]
        public long LocalContentId { get; set; }
        
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;
        
        // Stored as long because FFXIV's AccountId is a 64-bit ulong (per
        // FFXIVClientStructs Character.AccountId / SpawnPlayerPacket.AccountId).
        // The earlier int? was truncating to the lower 32 bits, which collapsed
        // distinct accounts into shared int32 buckets and produced what looked
        // like alt-linkage leakage at scale. Bit-pattern is preserved across the
        // ulong → long unchecked cast performed at the plugin scan sites; equality
        // matching for alt grouping works the same regardless of signed/unsigned.
        public long? AccountId { get; set; }
        public short? HomeWorldId { get; set; }
        public short? CurrentWorldId { get; set; }
        public short? TerritoryId { get; set; }
        
        public byte? CurrentJobId { get; set; }
        public short? CurrentJobLevel { get; set; }
        
        [MaxLength(100)]
        public string? PlayerPos { get; set; }
        
        [MaxLength(500)]
        public string? AvatarLink { get; set; }
        
        public DateTime? LastScannedAt { get; set; }
        
        // Lodestone JSON blobs are unbounded — a single player can own 400+ minions/mounts and
        // serialize past the old 10k cap. Use the Postgres `text` type (no length limit) via
        // Column(TypeName = "text") so we never truncate or 500 on long collections.
        [Column(TypeName = "text")]
        public string? LodestoneJobData { get; set; }

        public byte? MainJobId { get; set; }
        public short? MainJobLevel { get; set; }
        public DateTime? LastJobDataUpdate { get; set; }

        [Column(TypeName = "text")]
        public string? LodestoneMinionsData { get; set; }
        public DateTime? LastMinionsDataUpdate { get; set; }

        [Column(TypeName = "text")]
        public string? LodestoneMountsData { get; set; }
        public DateTime? LastMountsDataUpdate { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        
        // Privacy flags
        public bool IsPrivate { get; set; } = false;
        public bool HideInSearch { get; set; } = false;

        // Ownership + extended privacy (added in Plan 0a for web-app tier-gating)
        public bool HideAlts { get; set; } = false;
        public bool HideEncounters { get; set; } = false;
        public bool HideEntirely { get; set; } = false;

        public int? ClaimedByUserId { get; set; }
        public DateTime? ClaimedAt { get; set; }
        public DateTime? ClaimVerifiedAt { get; set; }

        [ForeignKey(nameof(ClaimedByUserId))]
        public virtual ApplicationUser? ClaimedByUser { get; set; }

        // Navigation properties
        public virtual ICollection<PlayerNameHistory> NameHistory { get; set; } = new List<PlayerNameHistory>();
        public virtual ICollection<PlayerWorldHistory> WorldHistory { get; set; } = new List<PlayerWorldHistory>();
        public virtual ICollection<PlayerCustomizationHistory> CustomizationHistory { get; set; } = new List<PlayerCustomizationHistory>();
        public virtual ICollection<PlayerTerritoryHistory> TerritoryHistory { get; set; } = new List<PlayerTerritoryHistory>();
        public virtual PlayerLodestone? Lodestone { get; set; }
        public virtual ICollection<PlayerProfileVisit> ProfileVisits { get; set; } = new List<PlayerProfileVisit>();
    }

    [Table("PlayerNameHistory")]
    public class PlayerNameHistory
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public long PlayerLocalContentId { get; set; }
        
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;
        
        public DateTime CreatedAt { get; set; }
        
        [ForeignKey(nameof(PlayerLocalContentId))]
        public virtual Player Player { get; set; } = null!;
    }

    [Table("PlayerWorldHistory")]
    public class PlayerWorldHistory
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public long PlayerLocalContentId { get; set; }
        
        [Required]
        public short WorldId { get; set; }
        
        public DateTime CreatedAt { get; set; }
        
        [ForeignKey(nameof(PlayerLocalContentId))]
        public virtual Player Player { get; set; } = null!;
    }

    [Table("PlayerCustomizationHistory")]
    public class PlayerCustomizationHistory
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public long PlayerLocalContentId { get; set; }
        
        public byte? BodyType { get; set; }
        public byte? GenderRace { get; set; }
        public byte? Height { get; set; }
        public byte? Face { get; set; }
        public byte? SkinColor { get; set; }
        public byte? Nose { get; set; }
        public byte? Jaw { get; set; }
        public byte? MuscleMass { get; set; }
        public byte? BustSize { get; set; }
        public byte? TailShape { get; set; }
        public byte? Mouth { get; set; }
        public byte? EyeShape { get; set; }
        public bool? SmallIris { get; set; }
        
        public DateTime CreatedAt { get; set; }
        
        [ForeignKey(nameof(PlayerLocalContentId))]
        public virtual Player Player { get; set; } = null!;
    }

    [Table("PlayerTerritoryHistory")]
    public class PlayerTerritoryHistory
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public long PlayerLocalContentId { get; set; }
        
        public short? TerritoryId { get; set; }
        
        [MaxLength(100)]
        public string? PlayerPos { get; set; }
        
        public short? WorldId { get; set; }
        public DateTime FirstSeenAt { get; set; }
        public DateTime LastSeenAt { get; set; }
        
        [ForeignKey(nameof(PlayerLocalContentId))]
        public virtual Player Player { get; set; } = null!;
    }

    [Table("PlayerLodestone")]
    public class PlayerLodestone
    {
        [Key]
        public long PlayerLocalContentId { get; set; }
        
        public int? LodestoneId { get; set; }
        public DateTime? CharacterCreationDate { get; set; }
        
        [MaxLength(500)]
        public string? AvatarLink { get; set; }
        
        [ForeignKey(nameof(PlayerLocalContentId))]
        public virtual Player Player { get; set; } = null!;
    }

    [Table("PlayerProfileVisits")]
    public class PlayerProfileVisit
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public long PlayerLocalContentId { get; set; }
        
        [MaxLength(100)]
        public string? VisitorId { get; set; }
        
        public DateTime VisitedAt { get; set; }
        
        [ForeignKey(nameof(PlayerLocalContentId))]
        public virtual Player Player { get; set; } = null!;
    }
}