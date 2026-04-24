namespace AlphaScopeServer.Services.World;

public static class WorldNames
{
    private static readonly Dictionary<short, string> Map = new()
    {
        // Aether (NA)
        { 34, "Brynhildr" }, { 62, "Diabolos" }, { 75, "Malboro" }, { 37, "Mateus" },
        { 73, "Adamantoise" }, { 79, "Cactuar" }, { 54, "Faerie" }, { 63, "Gilgamesh" },
        { 40, "Jenova" }, { 65, "Midgardsormr" }, { 99, "Sargatanas" }, { 57, "Siren" },

        // Primal (NA)
        { 53, "Exodus" }, { 78, "Behemoth" }, { 93, "Excalibur" }, { 35, "Famfrit" },
        { 95, "Hyperion" }, { 55, "Lamia" }, { 64, "Leviathan" }, { 77, "Ultros" },

        // Crystal (NA)
        { 91, "Balmung" }, { 81, "Goblin" }, { 41, "Zalera" }, { 74, "Coeurl" },

        // Chaos (EU)
        { 80, "Cerberus" }, { 71, "Moogle" }, { 39, "Omega" }, { 97, "Ragnarok" },
        { 85, "Spriggan" },

        // Light (EU)
        { 36, "Lich" }, { 66, "Odin" }, { 56, "Phoenix" }, { 67, "Shiva" },
        { 33, "Twintania" },

        // Elemental (JP)
        { 23, "Asura" }, { 45, "Carbuncle" }, { 58, "Garuda" }, { 59, "Ifrit" },
        { 49, "Kujata" }, { 50, "Typhon" },

        // Gaia (JP)
        { 43, "Alexander" }, { 69, "Bahamut" }, { 92, "Durandal" }, { 46, "Fenrir" },
        { 51, "Ultima" }, { 98, "Ridill" },

        // Mana (JP)
        { 44, "Anima" }, { 70, "Chocobo" }, { 47, "Hades" }, { 48, "Ixion" },
        { 96, "Masamune" }, { 61, "Titan" }, { 28, "Pandaemonium" },

        // Meteor (JP)
        { 24, "Belias" }, { 82, "Mandragora" }, { 60, "Ramuh" }, { 29, "Shinryu" },
        { 52, "Valefor" }, { 30, "Unicorn" }, { 31, "Yojimbo" }, { 32, "Zeromus" },

        // Materia (OCE)
        { 21, "Ravana" }, { 22, "Bismarck" }, { 86, "Sephirot" }, { 87, "Sophia" }, { 88, "Zurvan" },
    };

    private static readonly Dictionary<string, short> SlugToId = BuildSlugToId();

    private static Dictionary<string, short> BuildSlugToId()
    {
        var dict = new Dictionary<string, short>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in Map)
        {
            var slug = ToSlug(kvp.Value);
            dict.TryAdd(slug, kvp.Key);
        }
        return dict;
    }

    public static string ToSlug(string input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return System.Text.RegularExpressions.Regex.Replace(
            input.Trim().ToLowerInvariant().Replace("'", ""),
            @"\s+", "-");
    }

    public static string? Resolve(short? worldId)
        => worldId.HasValue && Map.TryGetValue(worldId.Value, out var name) ? name : null;

    public static short? ResolveFromSlug(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug)) return null;
        return SlugToId.TryGetValue(slug, out var id) ? id : null;
    }

    public static IReadOnlyCollection<string> AllSlugs() => SlugToId.Keys.ToList();
    public static IReadOnlyDictionary<short, string> AllNames() => Map;
}
