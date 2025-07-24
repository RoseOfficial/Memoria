using System.ComponentModel.DataAnnotations;

namespace AlphaScope.Database;

public class Player
{
    [Key, Required]
    public ulong LocalContentId { get; set; }

    [MaxLength(20), Required]
    public string? Name { get; set; }

    public ulong? AccountId { get; set; }

    public byte? CurrentJobId { get; set; }

    public short? CurrentJobLevel { get; set; }
}
