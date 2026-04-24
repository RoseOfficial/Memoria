using Newtonsoft.Json;

namespace MemoriaServer.Models.DTOs
{
    public class PaginationBase<T>
    {
        [JsonProperty("L")] public int LastCursor { get; set; }
        [JsonProperty("N")] public int NextCount { get; set; }
        [JsonProperty("D")] public List<T> Data { get; set; } = new();
    }
}