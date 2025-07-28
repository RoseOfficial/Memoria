using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AlphaScopeServer.Models.Entities
{
    [Table("Players")]
    public class Player
    {
        [Key]
        public long LocalContentId { get; set; }
        
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;
        
        public int? AccountId { get; set; }
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
        
        [MaxLength(2000)]
        public string? LodestoneJobData { get; set; }
        
        public byte? MainJobId { get; set; }
        public short? MainJobLevel { get; set; }
        public DateTime? LastJobDataUpdate { get; set; }
        
        [MaxLength(10000)]
        public string? LodestoneMinionsData { get; set; }
        public DateTime? LastMinionsDataUpdate { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        
        // Privacy flags
        public bool IsPrivate { get; set; } = false;
        public bool HideInSearch { get; set; } = false;
        
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