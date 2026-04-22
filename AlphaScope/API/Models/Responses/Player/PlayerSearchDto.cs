using Newtonsoft.Json;
using System;

namespace AlphaScope.API.Models.Responses.Player
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

        [JsonProperty("HomeWorldId")] public short? HomeWorldId { get; set; }
        [JsonProperty("LastScannedAt")] public DateTime? LastScannedAt { get; set; }
        [JsonProperty("LodestoneJobData")] public string? LodestoneJobData { get; set; }
        [JsonProperty("MainJobId")] public byte? MainJobId { get; set; }
        [JsonProperty("MainJobLevel")] public short? MainJobLevel { get; set; }
        [JsonProperty("LastJobDataUpdate")] public DateTime? LastJobDataUpdate { get; set; }
        [JsonProperty("LodestoneMinionsData")] public string? LodestoneMinionsData { get; set; }
        [JsonProperty("LastMinionsDataUpdate")] public DateTime? LastMinionsDataUpdate { get; set; }
        [JsonProperty("LodestoneMountsData")] public string? LodestoneMountsData { get; set; }
        [JsonProperty("LastMountsDataUpdate")] public DateTime? LastMountsDataUpdate { get; set; }
    }
}
