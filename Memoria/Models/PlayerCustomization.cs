using Newtonsoft.Json;

namespace Memoria.Models
{
    /// <summary>
    /// Compact customization payload sent on every player scan. Property keys mirror
    /// MemoriaServer.Models.DTOs.PlayerCustomization on the server side. Attributes
    /// MUST be Newtonsoft's <c>JsonProperty</c> — the upload pipeline uses
    /// RestSharp.Serializers.NewtonsoftJson, which ignores System.Text.Json's
    /// <c>JsonPropertyName</c> and falls back to C# property names. The earlier
    /// JsonPropertyName attribution silently shipped every field as a different key
    /// than the server expected, so customization arrived as all-nulls on every
    /// scan and the profile's Customization panel rendered blank.
    /// </summary>
    public class PlayerCustomization
    {
        [JsonProperty("1")]
        public byte? BodyType { get; set; }
        [JsonProperty("2")]
        public byte? GenderRace { get; set; }
        [JsonProperty("3")]
        public byte? Height { get; set; }
        [JsonProperty("4")]
        public byte? Face { get; set; }
        [JsonProperty("5")]
        public byte? SkinColor { get; set; }
        [JsonProperty("6")]
        public byte? Nose { get; set; }
        [JsonProperty("7")]
        public byte? Jaw { get; set; }
        [JsonProperty("8")]
        public byte? MuscleMass { get; set; }
        [JsonProperty("9")]
        public byte? BustSize { get; set; }
        [JsonProperty("A")]
        public byte? TailShape { get; set; }
        [JsonProperty("B")]
        public byte? Mouth { get; set; }
        [JsonProperty("C")]
        public byte? EyeShape { get; set; }
        [JsonProperty("D")]
        public bool? SmallIris { get; set; }
    }
}
