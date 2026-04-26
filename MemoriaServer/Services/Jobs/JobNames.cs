namespace MemoriaServer.Services.Jobs;

/// <summary>
/// Resolves FFXIV ClassJob row IDs (1–42) to display names. Mirrors the plugin's
/// `Utils.GetJobName` (which uses Lumina at runtime) but lives on the server where
/// Lumina isn't available. Manually maintained — Square Enix adds at most one or
/// two jobs per expansion, so the upkeep cost is trivial.
///
/// IDs come from the in-game ClassJob sheet. Indexes 1–18 are the original 2.0
/// classes, 19+ are jobs (with class upgrades like Gladiator → Paladin), then
/// each expansion appends new jobs at the end of the sheet.
/// </summary>
public static class JobNames
{
    private static readonly Dictionary<byte, string> Map = new()
    {
        // 2.0 classes
        { 1, "Gladiator" }, { 2, "Pugilist" }, { 3, "Marauder" }, { 4, "Lancer" },
        { 5, "Archer" }, { 6, "Conjurer" }, { 7, "Thaumaturge" },

        // Disciples of the Hand (crafters)
        { 8, "Carpenter" }, { 9, "Blacksmith" }, { 10, "Armorer" }, { 11, "Goldsmith" },
        { 12, "Leatherworker" }, { 13, "Weaver" }, { 14, "Alchemist" }, { 15, "Culinarian" },

        // Disciples of the Land (gatherers)
        { 16, "Miner" }, { 17, "Botanist" }, { 18, "Fisher" },

        // 2.0 jobs
        { 19, "Paladin" }, { 20, "Monk" }, { 21, "Warrior" }, { 22, "Dragoon" },
        { 23, "Bard" }, { 24, "White Mage" }, { 25, "Black Mage" },

        // 2.x jobs
        { 26, "Arcanist" }, { 27, "Summoner" }, { 28, "Scholar" },

        // Heavensward
        { 29, "Rogue" }, { 30, "Ninja" }, { 31, "Machinist" }, { 32, "Dark Knight" },
        { 33, "Astrologian" },

        // Stormblood
        { 34, "Red Mage" }, { 35, "Samurai" }, { 36, "Blue Mage" },

        // Shadowbringers
        { 37, "Gunbreaker" }, { 38, "Dancer" },

        // Endwalker
        { 39, "Reaper" }, { 40, "Sage" },

        // Dawntrail
        { 41, "Viper" }, { 42, "Pictomancer" },
    };

    /// <summary>Returns the display name for a job/class id, or null if unknown.</summary>
    public static string? Resolve(byte? jobId)
        => jobId.HasValue && Map.TryGetValue(jobId.Value, out var name) ? name : null;

    /// <summary>Exposed for tests so the parity check can iterate the canonical set.</summary>
    public static IReadOnlyDictionary<byte, string> All() => Map;
}
