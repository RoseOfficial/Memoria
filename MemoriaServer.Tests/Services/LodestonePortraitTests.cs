using FluentAssertions;
using MemoriaServer.Services.Lodestone;
using Xunit;

namespace MemoriaServer.Tests.Services;

public class LodestonePortraitTests
{
    [Fact]
    public void DeriveFullPortraitFromAvatar_strips_size_suffix()
    {
        var avatar = "https://img2.finalfantasyxiv.com/f/abc123def456_96x96.jpg";
        var portrait = LodestoneEnrichmentService.DeriveFullPortraitFromAvatar(avatar);
        portrait.Should().Be("https://img2.finalfantasyxiv.com/f/abc123def456.jpg");
    }

    [Fact]
    public void DeriveFullPortraitFromAvatar_returns_null_for_null_input()
    {
        LodestoneEnrichmentService.DeriveFullPortraitFromAvatar(null).Should().BeNull();
    }

    [Fact]
    public void DeriveFullPortraitFromAvatar_returns_null_for_empty_input()
    {
        LodestoneEnrichmentService.DeriveFullPortraitFromAvatar("").Should().BeNull();
    }

    [Fact]
    public void DeriveFullPortraitFromAvatar_returns_null_when_pattern_doesnt_match()
    {
        // If the URL doesn't have a "_NxN" suffix, we have no way to derive the
        // full portrait. Return null so the caller treats it as "no portrait
        // available" rather than risking the unmodified URL being used in
        // portrait position on the profile.
        var weird = "https://example.com/some_picture.png";
        LodestoneEnrichmentService.DeriveFullPortraitFromAvatar(weird).Should().BeNull();
    }

    [Fact]
    public void DeriveFullPortraitFromAvatar_handles_alternate_size_suffixes()
    {
        // Lodestone has shipped both _96x96 and _64x64 historically.
        var avatar = "https://img2.finalfantasyxiv.com/f/xyz_64x64.jpg";
        var portrait = LodestoneEnrichmentService.DeriveFullPortraitFromAvatar(avatar);
        portrait.Should().Be("https://img2.finalfantasyxiv.com/f/xyz.jpg");
    }
}
