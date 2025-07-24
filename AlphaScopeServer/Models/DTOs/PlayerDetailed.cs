using Newtonsoft.Json;

namespace AlphaScopeServer.Models.DTOs
{
    public class PlayerDetailed
    {
        [JsonProperty("F")] public int[] Flags { get; set; } = [];
        [JsonProperty("1")] public long LocalContentId { get; set; }
        [JsonProperty("2")] public int? AccountId { get; set; }
        [JsonProperty("3")] public List<PlayerCustomizationHistoryDto> PlayerCustomizationHistories { get; set; } = new();
        [JsonProperty("4")] public List<PlayerTerritoryHistoryDto> TerritoryHistory { get; set; } = new();
        [JsonProperty("5")] public PlayerLodestoneDto? PlayerLodestone { get; set; } = null;
        [JsonProperty("6")] public List<PlayerNameHistoryDto> PlayerNameHistories { get; set; } = new();
        [JsonProperty("7")] public List<PlayerWorldHistoryDto> PlayerWorldHistories { get; set; } = new();
        [JsonProperty("8")] public List<RetainerDetailedDto> Retainers { get; set; } = new();
        [JsonProperty("9")] public List<PlayerDetailedInfoAltCharDto> PlayerAltCharacters { get; set; } = new();
        [JsonProperty("0")] public PlayerProfileVisitInfoDto? ProfileVisitInfo { get; set; } = null;
        [JsonProperty("J")] public byte? CurrentJobId { get; set; }
        [JsonProperty("K")] public short? CurrentJobLevel { get; set; }
    }

    public class PlayerProfileVisitInfoDto
    {
        [JsonProperty("1")] public int? ProfileTotalVisitCount { get; set; } = 0;
        [JsonProperty("2")] public int? LastProfileVisitDate { get; set; } = 0;
        [JsonProperty("3")] public int? UniqueVisitorCount { get; set; } = 0;
    }

    public class PlayerCustomizationHistoryDto
    {
        [JsonProperty("1")] public byte? BodyType { get; set; }
        [JsonProperty("2")] public byte? GenderRace { get; set; }
        [JsonProperty("3")] public byte? Height { get; set; }
        [JsonProperty("4")] public byte? Face { get; set; }
        [JsonProperty("5")] public byte? SkinColor { get; set; }
        [JsonProperty("6")] public byte? Nose { get; set; }
        [JsonProperty("7")] public byte? Jaw { get; set; }
        [JsonProperty("8")] public byte? MuscleMass { get; set; }
        [JsonProperty("9")] public byte? BustSize { get; set; }
        [JsonProperty("0")] public byte? TailShape { get; set; }
        [JsonProperty("A")] public byte? Mouth { get; set; }
        [JsonProperty("B")] public byte? EyeShape { get; set; }
        [JsonProperty("C")] public bool? SmallIris { get; set; }
        [JsonProperty("D")] public int? CreatedAt { get; set; }
    }

    public class PlayerTerritoryHistoryDto
    {
        [JsonProperty("1")] public short? TerritoryId { get; set; }
        [JsonProperty("2")] public string? PlayerPos { get; set; }
        [JsonProperty("3")] public short? WorldId { get; set; }
        [JsonProperty("4")] public int? FirstSeenAt { get; set; }
        [JsonProperty("5")] public int? LastSeenAt { get; set; }
    }

    public class PlayerLodestoneDto
    {
        [JsonProperty("1")] public int? LodestoneId { get; set; }
        [JsonProperty("2")] public int? CharacterCreationDate { get; set; }
        [JsonProperty("3")] public string? AvatarLink { get; set; }
    }

    public class PlayerDetailedInfoAltCharDto
    {
        [JsonProperty("1")] public long LocalContentId { get; set; }
        [JsonProperty("2")] public string? Name { get; set; }
        [JsonProperty("3")] public short? WorldId { get; set; }
        [JsonProperty("4")] public List<RetainerDetailedDto> Retainers { get; set; } = new();
        [JsonProperty("5")] public string? AvatarLink { get; set; }
    }

    public class PlayerNameHistoryDto
    {
        [JsonProperty("V")] public string Name { get; set; } = null!;
        [JsonProperty("A")] public int CreatedAt { get; set; }
    }

    public class PlayerWorldHistoryDto
    {
        [JsonProperty("V")] public int WorldId { get; set; }
        [JsonProperty("A")] public int CreatedAt { get; set; }
    }

    public class RetainerDetailedDto
    {
        [JsonProperty("1")] public long LocalContentId { get; set; }
        [JsonProperty("2")] public long OwnerLocalContentId { get; set; }
        [JsonProperty("3")] public int LastSeen { get; set; }
        [JsonProperty("4")] public List<RetainerNameHistoryDto> Names { get; set; } = new();
        [JsonProperty("5")] public List<RetainerWorldHistoryDto> Worlds { get; set; } = new();
    }

    public class RetainerNameHistoryDto
    {
        [JsonProperty("V")] public string Name { get; set; } = null!;
        [JsonProperty("A")] public int CreatedAt { get; set; }
    }

    public class RetainerWorldHistoryDto
    {
        [JsonProperty("V")] public int WorldId { get; set; }
        [JsonProperty("A")] public int CreatedAt { get; set; }
    }
}