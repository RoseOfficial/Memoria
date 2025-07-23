using Newtonsoft.Json;

namespace PlayerScopeServer.Models.DTOs
{
    public class PlayerDto
    {
        [JsonProperty("L")] public long LocalContentId { get; set; }
        [JsonProperty("N")] public string Name { get; set; } = string.Empty;
        [JsonProperty("A")] public int? AccountId { get; set; }
        [JsonProperty("B")] public string? AvatarLink { get; set; }
    }
}