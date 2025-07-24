using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AlphaScopeServer.Models.Entities
{
    [Table("Retainers")]
    public class Retainer
    {
        [Key]
        public long LocalContentId { get; set; }
        
        [Required]
        [MaxLength(24)]
        public string Name { get; set; } = string.Empty;
        
        [Required]
        public short WorldId { get; set; }
        
        [Required]
        public long OwnerLocalContentId { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastSeen { get; set; } = DateTime.UtcNow;
        
        // Navigation properties
        [ForeignKey(nameof(OwnerLocalContentId))]
        public virtual Player Owner { get; set; } = null!;
        
        public virtual ICollection<RetainerNameHistory> NameHistory { get; set; } = new List<RetainerNameHistory>();
        public virtual ICollection<RetainerWorldHistory> WorldHistory { get; set; } = new List<RetainerWorldHistory>();
    }

    [Table("RetainerNameHistory")]
    public class RetainerNameHistory
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public long RetainerLocalContentId { get; set; }
        
        [Required]
        [MaxLength(24)]
        public string Name { get; set; } = string.Empty;
        
        public DateTime CreatedAt { get; set; }
        
        [ForeignKey(nameof(RetainerLocalContentId))]
        public virtual Retainer Retainer { get; set; } = null!;
    }

    [Table("RetainerWorldHistory")]
    public class RetainerWorldHistory
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public long RetainerLocalContentId { get; set; }
        
        [Required]
        public short WorldId { get; set; }
        
        public DateTime CreatedAt { get; set; }
        
        [ForeignKey(nameof(RetainerLocalContentId))]
        public virtual Retainer Retainer { get; set; } = null!;
    }
}