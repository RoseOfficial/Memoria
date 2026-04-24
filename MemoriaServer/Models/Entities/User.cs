using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MemoriaServer.Models.Entities
{
    [Table("Users")]
    public class ApplicationUser
    {
        [Key]
        public int Id { get; set; }
        
        // Nullable: web-first users created via Discord OAuth have no GameAccountId until
        // they redeem an account-link code from the plugin.
        public int? GameAccountId { get; set; }

        [Required]
        public long PrimaryCharacterLocalContentId { get; set; }
        
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(256)]
        public string ApiKey { get; set; } = string.Empty;
        
        public int AppRoleId { get; set; } = 1; // Default to Member role
        
        [MaxLength(500)]
        public string? BaseUrl { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastLoginAt { get; set; } = DateTime.UtcNow;

        // Discord identity (set by OAuth callback in Plan 0a).
        public long? DiscordUserId { get; set; }
        public bool IsGuildMember { get; set; } = false;
        public DateTime? GuildMembershipCheckedAt { get; set; }

        // Statistics
        public int UploadedPlayersCount { get; set; } = 0;
        public int UploadedPlayerInfoCount { get; set; } = 0;
        public int UploadedRetainersCount { get; set; } = 0;
        public int UploadedRetainerInfoCount { get; set; } = 0;
        public int FetchedPlayerInfoCount { get; set; } = 0;
        public int SearchedNamesCount { get; set; } = 0;
        public DateTime LastSyncedTime { get; set; } = DateTime.UtcNow;

        // Lifetime scan contribution count. Incremented by PlayersController on each
        // accepted scan upload. Surfaced via GET v1/users/me/contributions.
        public int TotalContributions { get; set; } = 0;
        
        // Navigation properties
        public virtual ICollection<UserCharacter> Characters { get; set; } = new List<UserCharacter>();
    }

    [Table("UserCharacters")]
    public class UserCharacter
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public int UserId { get; set; }
        
        [Required]
        public long LocalContentId { get; set; }
        
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;
        
        [MaxLength(500)]
        public string? AvatarLink { get; set; }
        
        // Privacy settings
        public bool HideFullProfile { get; set; } = false;
        public bool HideTerritoryInfo { get; set; } = false;
        public bool HideCustomizations { get; set; } = false;
        public bool HideInSearchResults { get; set; } = false;
        public bool HideRetainersInfo { get; set; } = false;
        public bool HideAltCharacters { get; set; } = false;
        
        // Profile visit stats
        public int ProfileTotalVisitCount { get; set; } = 0;
        public DateTime? LastProfileVisitDate { get; set; }
        
        [ForeignKey(nameof(UserId))]
        public virtual ApplicationUser User { get; set; } = null!;
    }

    // Enum for user roles
    public enum UserRole
    {
        Banned = -1,
        Guest = 0,
        Member = 1,
        Verified = 2,
        VIP = 5,
        Moderator = 8,
        Admin = 9,
        Owner = 10
    }
}