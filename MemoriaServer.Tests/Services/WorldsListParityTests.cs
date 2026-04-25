using System.IO;
using System.Text.RegularExpressions;
using FluentAssertions;
using MemoriaServer.Services.World;
using Xunit;

namespace MemoriaServer.Tests.Services;

/// <summary>
/// Asserts the canonical world list at web/lib/worlds.ts mirrors WorldNames.Map
/// exactly. The web's claim form uses the TS list to populate its dropdown; if
/// the two drift, users either can't pick their world (web missing a server
/// entry) or pick a slug the server doesn't recognise (web has phantom worlds).
/// </summary>
public class WorldsListParityTests
{
    [Fact]
    public void WebWorldsList_MatchesServerMap()
    {
        var path = Path.Combine(FindRepoRoot(), "web", "lib", "worlds.ts");
        File.Exists(path).Should().BeTrue($"expected web worlds list at {path}");

        var text = File.ReadAllText(path);

        // Match entries shaped like: { id: 34, name: 'Brynhildr', slug: 'brynhildr', dataCenter: 'Aether' },
        // The format guarantee is documented at the top of worlds.ts.
        var entryRegex = new Regex(
            @"\{\s*id:\s*(?<id>\d+),\s*name:\s*'(?<name>[^']+)',\s*slug:\s*'(?<slug>[^']+)',\s*dataCenter:\s*'(?<dc>[^']+)'\s*\},",
            RegexOptions.Compiled);

        var webEntries = entryRegex.Matches(text)
            .Select(m => new
            {
                Id = short.Parse(m.Groups["id"].Value),
                Name = m.Groups["name"].Value,
                Slug = m.Groups["slug"].Value,
            })
            .ToDictionary(e => e.Id);

        var serverMap = WorldNames.AllNames();

        // Every server world must appear in web with the same id+name and a
        // slug equal to WorldNames.ToSlug(name).
        foreach (var (id, name) in serverMap)
        {
            webEntries.Should().ContainKey(id, $"server has world {id}={name} but web does not");
            var entry = webEntries[id];
            entry.Name.Should().Be(name, $"name mismatch for world id {id}");
            entry.Slug.Should().Be(WorldNames.ToSlug(name), $"slug mismatch for world id {id} ({name})");
        }

        // No phantom worlds in web that aren't on the server.
        foreach (var (id, entry) in webEntries)
        {
            serverMap.Should().ContainKey(id, $"web has world id {id}={entry.Name} but server does not");
        }

        webEntries.Count.Should().Be(serverMap.Count, "world counts should match exactly");
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Memoria.sln")))
            dir = dir.Parent;
        if (dir is null)
            throw new InvalidOperationException("Could not locate Memoria.sln walking up from " + AppContext.BaseDirectory);
        return dir.FullName;
    }
}
