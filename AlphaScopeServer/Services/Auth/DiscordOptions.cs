namespace AlphaScopeServer.Services.Auth
{
    /// <summary>
    /// Discord OAuth + guild-membership config. Bound from the "Discord" section of appsettings
    /// / env vars. All fields are required at server boot; missing values throw in Program.cs.
    /// </summary>
    public sealed class DiscordOptions
    {
        public const string Section = "Discord";

        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
        public string GuildId { get; set; } = string.Empty;
        public string StateSigningKey { get; set; } = string.Empty;
    }
}
