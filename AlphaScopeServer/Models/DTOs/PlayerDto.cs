using Newtonsoft.Json;

namespace AlphaScopeServer.Models.DTOs
{
    public class PlayerDto
    {
        [JsonProperty("L")] public long LocalContentId { get; set; }
        [JsonProperty("N")] public string Name { get; set; } = string.Empty;
        [JsonProperty("A")] public int? AccountId { get; set; }
        [JsonProperty("B")] public string? AvatarLink { get; set; }
        [JsonProperty("J")] public byte? CurrentJobId { get; set; }
        [JsonProperty("K")] public short? CurrentJobLevel { get; set; }
    }
}