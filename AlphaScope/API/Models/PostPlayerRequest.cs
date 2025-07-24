using Newtonsoft.Json;
using AlphaScope.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AlphaScope.API.Models
{
    public class PostPlayerRequest
    {
        [JsonProperty("1")]
        public ulong LocalContentId { get; set; }
        [JsonProperty("2")]
        public string Name { get; set; } = string.Empty;
        [JsonProperty("3")]
        public ushort? HomeWorldId { get; set; }
        [JsonProperty("4")]
        public int? AccountId { get; set; }
        [JsonProperty("5")]
        public short? TerritoryId { get; set; }
        [JsonProperty("6")]
        public string? PlayerPos { get; set; }
        [JsonProperty("7")]
        public ushort? CurrentWorldId { get; set; }
        [JsonProperty("8")]
        public PlayerCustomization? Customization { get; set; }
        [JsonProperty("9")]
        public int CreatedAt { get; set; }
        [JsonProperty("10")]
        public byte? CurrentJobId { get; set; }
        [JsonProperty("11")]
        public short? CurrentJobLevel { get; set; }
    }
}
