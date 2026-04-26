using FluentAssertions;
using MemoriaServer.Services.Jobs;
using Xunit;

namespace MemoriaServer.Tests.Services;

public class JobNamesTests
{
    [Theory]
    [InlineData((byte)1, "Gladiator")]
    [InlineData((byte)19, "Paladin")]
    [InlineData((byte)23, "Bard")]
    [InlineData((byte)24, "White Mage")]
    [InlineData((byte)32, "Dark Knight")]
    [InlineData((byte)40, "Sage")]
    [InlineData((byte)42, "Pictomancer")]
    public void Resolve_ReturnsCanonicalName(byte id, string expected)
    {
        JobNames.Resolve(id).Should().Be(expected);
    }

    [Fact]
    public void Resolve_NullOrUnknown_ReturnsNull()
    {
        JobNames.Resolve(null).Should().BeNull();
        JobNames.Resolve(0).Should().BeNull();
        JobNames.Resolve(255).Should().BeNull();
    }

    [Fact]
    public void All_CoversIds1Through42_WithNoGaps()
    {
        var map = JobNames.All();
        for (byte id = 1; id <= 42; id++)
            map.Should().ContainKey(id, $"job/class id {id} must have a canonical name");
        map.Should().HaveCount(42);
    }
}
