using Newtonsoft.Json;

namespace MemoriaServer.Models.DTOs
{
    public class PlayerDto
    {
        [JsonProperty("L")] public long LocalContentId { get; set; }
        [JsonProperty("N")] public string Name { get; set; } = string.Empty;
        [JsonProperty("A")] public long? AccountId { get; set; }
        [JsonProperty("B")] public string? AvatarLink { get; set; }
        [JsonProperty("J")] public byte? CurrentJobId { get; set; }
        [JsonProperty("K")] public short? CurrentJobLevel { get; set; }
        [JsonProperty("LJ")] public string? LodestoneJobData { get; set; }
        [JsonProperty("MJ")] public byte? MainJobId { get; set; }
        [JsonProperty("ML")] public short? MainJobLevel { get; set; }
        [JsonProperty("LU")] public DateTime? LastJobDataUpdate { get; set; }
    }
}