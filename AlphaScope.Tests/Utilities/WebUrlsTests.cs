using FluentAssertions;
using AlphaScope.Utilities;

namespace AlphaScope.Tests.Utilities;

public class WebUrlsTests
{
    private const string DefaultBase = "https://alphascope.app";

    [Fact]
    public void ProfileUrl_WithSimpleNameAndWorld_ReturnsExpectedSlug()
    {
        var url = WebUrls.ProfileUrl(DefaultBase, "Kael Thalindor", "Gilgamesh");

        url.Should().Be("https://alphascope.app/c/Kael-Thalindor@Gilgamesh");
    }

    [Fact]
    public void ProfileUrl_WithApostrophe_PercentEncodesIt()
    {
        var url = WebUrls.ProfileUrl(DefaultBase, "Y'shtola Rhul", "Mateus");

        url.Should().Be("https://alphascope.app/c/Y%27shtola-Rhul@Mateus");
    }

    [Fact]
    public void ProfileUrl_TrimsTrailingSlashFromBase()
    {
        var url = WebUrls.ProfileUrl("https://alphascope.app/", "A B", "Faerie");

        url.Should().Be("https://alphascope.app/c/A-B@Faerie");
    }

    [Fact]
    public void MeUrl_ReturnsExpectedPath()
    {
        WebUrls.MeUrl(DefaultBase).Should().Be("https://alphascope.app/me");
    }

    [Fact]
    public void LandingUrl_ReturnsBaseUrl()
    {
        WebUrls.LandingUrl(DefaultBase).Should().Be("https://alphascope.app");
    }

    [Fact]
    public void ProfileUrl_WithEmptyName_Throws()
    {
        Action act = () => WebUrls.ProfileUrl(DefaultBase, "", "Gilgamesh");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ProfileUrl_WithEmptyWorld_Throws()
    {
        Action act = () => WebUrls.ProfileUrl(DefaultBase, "Kael Thalindor", "");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ProfileUrl_WithNullBaseUrl_Throws()
    {
        Action act = () => WebUrls.ProfileUrl(null!, "Kael Thalindor", "Gilgamesh");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MeUrl_WithEmptyBaseUrl_Throws()
    {
        Action act = () => WebUrls.MeUrl("");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void LandingUrl_WithWhitespaceBaseUrl_Throws()
    {
        Action act = () => WebUrls.LandingUrl("   ");

        act.Should().Throw<ArgumentException>();
    }
}
