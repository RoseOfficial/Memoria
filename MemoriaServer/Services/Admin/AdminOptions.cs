namespace MemoriaServer.Services.Admin;

public class AdminOptions
{
    public const string SectionName = "Admin";
    public List<long> DiscordUserIds { get; set; } = new();
}
