using FluentAssertions;

namespace AlphaScope.Tests;

public class SimpleTestExample
{
    [Fact]
    public void TestInfrastructure_ShouldWork()
    {
        var result = 2 + 2;
        result.Should().Be(4);
    }

    [Theory]
    [InlineData(1, 1, 2)]
    [InlineData(5, 3, 8)]
    [InlineData(-1, 1, 0)]
    public void Addition_ShouldReturnCorrectResult(int a, int b, int expected)
    {
        var result = a + b;
        result.Should().Be(expected);
    }
}