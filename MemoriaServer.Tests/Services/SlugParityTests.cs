using MemoriaServer.Services.World;
using FluentAssertions;
using Xunit;

namespace MemoriaServer.Tests.Services;

public class SlugParityTests
{
    [Theory]
    [InlineData("Balmung", "balmung")]
    [InlineData("Gilgamesh", "gilgamesh")]
    [InlineData("Tataru Taru", "tataru-taru")]  // player name
    [InlineData("T'chai Nunh", "tchai-nunh")]    // apostrophe
    [InlineData("Sha-lian Arazi", "sha-lian-arazi")]  // hyphen survives
    [InlineData("  Balmung  ", "balmung")]       // trim
    public void ToSlug_MatchesExpectedFormat(string input, string expected)
    {
        WorldNames.ToSlug(input).Should().Be(expected);
    }
}
