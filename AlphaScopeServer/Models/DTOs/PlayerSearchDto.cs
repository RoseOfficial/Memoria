using Newtonsoft.Json;

namespace AlphaScopeServer.Models.DTOs
{
    public class PlayerSearchDto
    {
        [JsonProperty("L")] public long LocalContentId { get; set; }
        [JsonProperty("N")] public string Name { get; set; } = string.Empty;
        [JsonProperty("W")] public short? WorldId { get; set; }
        [JsonProperty("A")] public int? AccountId { get; set; }
        [JsonProperty("B")] public string? AvatarLink { get; set; }
        [JsonProperty("J")] public byte? CurrentJobId { get; set; }
        [JsonProperty("K")] public short? CurrentJobLevel { get; set; }
    }
}