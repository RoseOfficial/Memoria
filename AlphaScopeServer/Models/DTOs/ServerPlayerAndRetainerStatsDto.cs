using Newtonsoft.Json;

namespace AlphaScopeServer.Models.DTOs
{
    public class ServerPlayerAndRetainerStatsDto
    {
        [JsonProperty("0")] public List<WorldCountStat> PlayerWorldStats { get; set; } = new();
        [JsonProperty("1")] public List<WorldCountStat> RetainerWorldStats { get; set; } = new();
        [JsonProperty("2")] public long LastUpdate { get; set; }
    }

    public class WorldCountStat
    {
        [JsonProperty("0")] public short WorldId { get; set; }
        [JsonProperty("1")] public int Count { get; set; }
    }
}