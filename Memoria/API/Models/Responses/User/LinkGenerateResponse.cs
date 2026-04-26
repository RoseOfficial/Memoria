using System;
using Newtonsoft.Json;

namespace Memoria.API.Models.Responses.User
{
    /// <summary>
    /// Response from POST /v1/auth/link/generate. The user pastes <see cref="Code"/> into the
    /// web app's /me/link page to merge their Discord identity onto this plugin install.
    /// </summary>
    public sealed class LinkGenerateResponse
    {
        [JsonProperty("code")] public string Code { get; set; } = string.Empty;
        [JsonProperty("expiresAt")] public DateTime ExpiresAt { get; set; }
    }
}
