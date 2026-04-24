using AlphaScopeServer.Services.World;
using FluentAssertions;
using Xunit;

namespace AlphaScopeServer.Tests.Services;

public class WorldNamesTests
{
    [Fact]
    public void Resolve_KnownId_ReturnsName()
    {
        WorldNames.Resolve(91).Should().Be("Balmung");
    }

    [Fact]
    public void Resolve_UnknownId_ReturnsNull()
    {
        WorldNames.Resolve(9999).Should().BeNull();
    }

    [Fact]
    public void Resolve_NullId_ReturnsNull()
    {
        WorldNames.Resolve(null).Should().BeNull();
    }

    [Theory]
    [InlineData("balmung", (short)91)]
    [InlineData("BALMUNG", (short)91)]
    [InlineData("Balmung", (short)91)]
    [InlineData("gilgamesh", (short)63)]
    public void ResolveFromSlug_KnownSlug_ReturnsId(string slug, short expected)
    {
        WorldNames.ResolveFromSlug(slug).Should().Be(expected);
    }

    [Fact]
    public void ResolveFromSlug_UnknownSlug_ReturnsNull()
    {
        WorldNames.ResolveFromSlug("notaworld").Should().BeNull();
    }

    [Fact]
    public void AllNames_AreUniqueAndCaseInsensitive()
    {
        var slugs = WorldNames.AllSlugs();
        slugs.Should().OnlyHaveUniqueItems();
        slugs.Should().OnlyContain(s => s == s.ToLowerInvariant());
    }
}
