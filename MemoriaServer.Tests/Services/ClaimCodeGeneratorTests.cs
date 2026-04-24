using FluentAssertions;
using MemoriaServer.Services.Auth;

namespace MemoriaServer.Tests.Services;

public class ClaimCodeGeneratorTests
{
    [Fact]
    public void GenerateClaimCode_ReturnsAsPrefixedCrockfordCode()
    {
        var code = ClaimCodeGenerator.GenerateClaimCode();

        code.Should().StartWith("AS-");
        code.Length.Should().Be(12); // "AS-XXXX-XXXX"
        code[7].Should().Be('-');

        var body = code.Substring(3).Replace("-", "");
        body.Should().MatchRegex("^[0-9A-HJKMNP-TV-Z]+$"); // Crockford: no I/L/O/U
    }

    [Fact]
    public void GenerateLinkCode_ReturnsAlPrefixedCrockfordCode()
    {
        var code = ClaimCodeGenerator.GenerateLinkCode();

        code.Should().StartWith("AL-");
        code.Length.Should().Be(12);
    }

    [Fact]
    public void GenerateClaimCode_DoesNotRepeatInShortSequence()
    {
        var codes = Enumerable.Range(0, 100).Select(_ => ClaimCodeGenerator.GenerateClaimCode()).ToList();
        codes.Distinct().Count().Should().Be(100);
    }
}
