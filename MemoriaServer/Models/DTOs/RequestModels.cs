using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace MemoriaServer.Models.DTOs
{
    public class PostPlayerRequest
    {
        [JsonProperty("1")] public ulong LocalContentId { get; set; }
        [JsonProperty("2")] public string Name { get; set; } = string.Empty;
        [JsonProperty("3")] public ushort? HomeWorldId { get; set; }
        [JsonProperty("4")] public long? AccountId { get; set; }
        [JsonProperty("5")] public short? TerritoryId { get; set; }
        [JsonProperty("6")] public string? PlayerPos { get; set; }
        [JsonProperty("7")] public ushort? CurrentWorldId { get; set; }
        [JsonProperty("8")] public PlayerCustomization? Customization { get; set; }
        [JsonProperty("9")] public int CreatedAt { get; set; }
        [JsonProperty("10")] public byte? CurrentJobId { get; set; }
        [JsonProperty("11")] public short? CurrentJobLevel { get; set; }
        [JsonProperty("12")] public string? LodestoneJobData { get; set; }
        [JsonProperty("13")] public byte? MainJobId { get; set; }
        [JsonProperty("14")] public short? MainJobLevel { get; set; }
        [JsonProperty("15")] public DateTime? LastJobDataUpdate { get; set; }
        [JsonProperty("16")] public string? LodestoneMinionsData { get; set; }
        [JsonProperty("17")] public DateTime? LastMinionsDataUpdate { get; set; }
        [JsonProperty("18")] public string? LodestoneMountsData { get; set; }
        [JsonProperty("19")] public DateTime? LastMountsDataUpdate { get; set; }
        [JsonProperty("20")] public string? TerritoryName { get; set; }

        // Phase 1 — captured by ObjectTableHandler (Title, OnlineStatus, Mount,
        // Minion, FC tag) and ProcessSocialListResult (GC, OnlineStatus, FC tag).
        // None are immutable per-character; "latest wins" overwrite semantics.
        [JsonProperty("21")] public byte? OnlineStatusId { get; set; }
        [JsonProperty("22")] public int? TitleId { get; set; }
        [JsonProperty("23")] public byte? GrandCompanyId { get; set; }
        [MaxLength(7), JsonProperty("24")] public string? FreeCompanyTag { get; set; }
        [JsonProperty("25")] public int? CurrentMountId { get; set; }
        [JsonProperty("26")] public int? CurrentMinionId { get; set; }
    }

    public class PostRetainerRequest
    {
        [Required, JsonProperty("L")] public ulong LocalContentId { get; set; }
        [MaxLength(24), Required, JsonProperty("N")] public string? Name { get; set; }
        [Required, JsonProperty("W")] public int WorldId { get; set; }
        [Required, JsonProperty("O")] public ulong OwnerLocalContentId { get; set; }
        [Required, JsonProperty("C")] public int CreatedAt { get; set; }
    }

    public class PlayerCustomization
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
        [JsonProperty("A")] public byte? TailShape { get; set; }
        [JsonProperty("B")] public byte? Mouth { get; set; }
        [JsonProperty("C")] public byte? EyeShape { get; set; }
        [JsonProperty("D")] public bool? SmallIris { get; set; }
    }

    public class UserRegister
    {
        public long GameAccountId { get; set; }
        public long UserLocalContentId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
    }

    public class UserUpdateDto
    {
        public List<UserCharacterDto?> Characters { get; set; } = new();
    }

    public class PlayerQueryObject
    {
        public long? LocalContentId { get; set; } = null;
        public string? Name { get; set; } = null;
        public int Cursor { get; set; } = 0;
        public bool IsFetching { get; set; }
        public List<short> F_WorldIds { get; set; } = new();
        public bool? F_MatchAnyPartOfName { get; set; } = false;
    }

    public class RetainerQueryObject
    {
        public string? Name { get; set; } = null;
        public int Cursor { get; set; } = 0;
        public bool IsFetching { get; set; }
        public List<short> F_WorldIds { get; set; } = new();
        public bool? F_MatchAnyPartOfName { get; set; } = false;
    }
}