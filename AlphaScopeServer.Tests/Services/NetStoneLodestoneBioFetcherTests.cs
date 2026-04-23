using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using AlphaScopeServer.Services.Lodestone;

namespace AlphaScopeServer.Tests.Services;

public class NetStoneLodestoneBioFetcherTests
{
    [Fact(Skip = "Integration: hits real Lodestone. Remove Skip to run locally.")]
    [Trait("Category", "Integration")]
    public async Task FetchBioAsync_ReturnsSuccess_ForKnownPublicCharacter()
    {
        // Lodestone id 1 is a well-known historical test character. If this ever 404s,
        // swap for any currently-active public character id.
        var fetcher = new NetStoneLodestoneBioFetcher(NullLogger<NetStoneLodestoneBioFetcher>.Instance);
        var result = await fetcher.FetchBioAsync(1, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Bio.Should().NotBeNull();
    }
}
