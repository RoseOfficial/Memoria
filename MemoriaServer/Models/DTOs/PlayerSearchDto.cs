using System;
using Newtonsoft.Json;

namespace MemoriaServer.Models.DTOs
{
    /// <summary>
    /// Row shape for <see cref="Controllers.PlayersController.SearchPlayers"/>. Carries everything
    /// the plugin's in-memory <c>CachedPlayer</c> needs so startup hydration rebuilds the cache in
    /// one round of calls without per-row detail fetches.
    /// </summary>
    public class PlayerSearchDto
    {
        [JsonProperty("L")] public long LocalContentId { get; set; }
        [JsonProperty("N")] public string Name { get; set; } = string.Empty;
        [JsonProperty("W")] public short? WorldId { get; set; }
        [JsonProperty("A")] public int? AccountId { get; set; }
        [JsonProperty("B")] public string? AvatarLink { get; set; }
        [JsonProperty("J")] public byte? CurrentJobId { get; set; }
        [JsonProperty("K")] public short? CurrentJobLevel { get; set; }

        // Fields below added so the plugin can rehydrate its cache from a single paginated call
        // rather than N follow-up GetById requests. Long names on purpose — readability beats
        // the handful of bytes saved at this field count.
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