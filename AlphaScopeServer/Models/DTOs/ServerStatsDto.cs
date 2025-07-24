using Newtonsoft.Json;

namespace AlphaScopeServer.Models.DTOs
{
    public class ServerStatsDto
    {
        [JsonProperty("1")] public int TotalPlayerCount { get; set; }
        [JsonProperty("2")] public int TotalPrivatePlayerCount { get; set; }
        [JsonProperty("3")] public int TotalRetainerCount { get; set; }
        [JsonProperty("4")] public int TotalPrivateRetainerCount { get; set; }
        [JsonProperty("5")] public int TotalUserCount { get; set; }
        [JsonProperty("6")] public long LastUpdate { get; set; }
    }
}