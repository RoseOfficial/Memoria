using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MemoriaServer.Models.Entities
{
    /// <summary>
    /// Server-side lookup of FFXIV TerritoryType id → display name. Populated
    /// passively by every scan upload that carries a non-null TerritoryName
    /// (the plugin resolves it from Lumina at scan time). The Locations panel
    /// joins this on TerritoryId to render real zone names instead of raw ids.
    ///
    /// One row per territory id; later scans with a different name (e.g. a Square
    /// Enix rename) overwrite the existing row.
    /// </summary>
    [Table("TerritoryNames")]
    public class TerritoryName
    {
        // TerritoryId is the FFXIV TerritoryType row id, not a server-generated id —
        // explicitly suppress the identity generation EF would otherwise apply to a
        // [Key] integer property.
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public short TerritoryId { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
