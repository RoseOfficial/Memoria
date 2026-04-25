namespace MemoriaServer.Services.Auth
{
    /// <summary>
    /// Discord OAuth + guild-membership config. Bound from the "Discord" section of appsettings
    /// / env vars. When any field is missing the server still boots, but Discord-dependent
    /// endpoints (auth/discord/*, auth/link/*) return 503. Lets operators stand up a public
    /// read-only deploy without provisioning a Discord app first.
    /// </summary>
    public sealed class DiscordOptions
    {
        public const string Section = "Discord";

        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
        public string GuildId { get; set; } = string.Empty;
        public string StateSigningKey { get; set; } = string.Empty;

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(ClientId) &&
            !string.IsNullOrWhiteSpace(ClientSecret) &&
            !string.IsNullOrWhiteSpace(GuildId) &&
            !string.IsNullOrWhiteSpace(StateSigningKey);
    }
}
