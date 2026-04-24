using AlphaScopeServer.Services.Takedowns;
using FluentAssertions;
using Xunit;

namespace AlphaScopeServer.Tests.Services;

public class TakedownRateLimiterTests
{
    [Fact]
    public void Allow_UnderLimit_ReturnsTrue()
    {
        var limiter = new TakedownRateLimiter();
        limiter.Allow("hash1").Should().BeTrue();
        limiter.Allow("hash1").Should().BeTrue();
        limiter.Allow("hash1").Should().BeTrue();
    }

    [Fact]
    public void Allow_OverLimit_ReturnsFalse()
    {
        var limiter = new TakedownRateLimiter();
        limiter.Allow("hash1");
        limiter.Allow("hash1");
        limiter.Allow("hash1");
        limiter.Allow("hash1").Should().BeFalse();
    }

    [Fact]
    public void Allow_DifferentHashes_Isolated()
    {
        var limiter = new TakedownRateLimiter();
        limiter.Allow("a"); limiter.Allow("a"); limiter.Allow("a");
        limiter.Allow("b").Should().BeTrue();
    }
}
