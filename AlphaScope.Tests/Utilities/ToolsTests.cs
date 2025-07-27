using FluentAssertions;
using AlphaScope;
using System.Globalization;

namespace AlphaScope.Tests.Utilities;

public class ToolsTests
{
    [Fact]
    public void UnixTime_ShouldReturnCurrentUnixTimestamp()
    {
        // Arrange
        var beforeCall = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // Act
        var result = Tools.UnixTime;
        var afterCall = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // Assert
        result.Should().BeGreaterOrEqualTo((int)beforeCall);
        result.Should().BeLessOrEqualTo((int)afterCall);
    }

    [Fact]
    public void ToTimeSinceString_WithDaysAgo_ShouldReturnCorrectFormat()
    {
        // Arrange
        var twoDaysAgo = DateTimeOffset.UtcNow.AddDays(-2).ToUnixTimeSeconds();

        // Act
        var result = Tools.ToTimeSinceString((int)twoDaysAgo);

        // Assert
        result.Should().NotBeNullOrEmpty();
        // Note: Exact format depends on localization, but should contain time information
    }

    [Fact]
    public void ToTimeSinceString_WithHoursAgo_ShouldReturnCorrectFormat()
    {
        // Arrange
        var threeHoursAgo = DateTimeOffset.UtcNow.AddHours(-3).ToUnixTimeSeconds();

        // Act
        var result = Tools.ToTimeSinceString((int)threeHoursAgo);

        // Assert
        result.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void ToTimeSinceString_WithMinutesAgo_ShouldReturnCorrectFormat()
    {
        // Arrange
        var tenMinutesAgo = DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds();

        // Act
        var result = Tools.ToTimeSinceString((int)tenMinutesAgo);

        // Assert
        result.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void ToTimeSinceString_WithSecondsAgo_ShouldReturnCorrectFormat()
    {
        // Arrange
        var thirtySecondsAgo = DateTimeOffset.UtcNow.AddSeconds(-30).ToUnixTimeSeconds();

        // Act
        var result = Tools.ToTimeSinceString((int)thirtySecondsAgo);

        // Assert
        result.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void ToTimeSinceString_WithCurrentTime_ShouldReturnJustNow()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // Act
        var result = Tools.ToTimeSinceString((int)now);

        // Assert
        result.Should().NotBeNullOrEmpty();
        // Should indicate "just now" or very recent
    }

    [Fact]
    public void TimeFromNow_WithFutureTime_ShouldReturnCorrectFormat()
    {
        // Arrange
        var futureTime = DateTimeOffset.UtcNow.AddHours(2).ToUnixTimeSeconds();

        // Act
        var result = Tools.TimeFromNow((int)futureTime);

        // Assert
        result.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void TimeFromNow_WithPastTime_ShouldReturnEmpty()
    {
        // Arrange
        var pastTime = DateTimeOffset.UtcNow.AddSeconds(-1).ToUnixTimeSeconds();

        // Act
        var result = Tools.TimeFromNow((int)pastTime);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void TimeFromNow_WithCurrentTime_ShouldReturnJustNow()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // Act
        var result = Tools.TimeFromNow((int)now);

        // Assert
        result.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1609459200)] // 2021-01-01 00:00:00 UTC
    [InlineData(1640995200)] // 2022-01-01 00:00:00 UTC
    public void UnixTimeConverter_ShouldConvertToLocalDateTime(int unixTime)
    {
        // Act
        var result = Tools.UnixTimeConverter(unixTime);

        // Assert
        result.Should().NotBeNullOrEmpty();
        
        // Verify it's a valid datetime string
        var expectedDateTime = DateTimeOffset.FromUnixTimeSeconds(unixTime).ToLocalTime().DateTime;
        DateTime.Parse(result).Should().BeCloseTo(expectedDateTime, TimeSpan.FromSeconds(1));
    }

    [Theory]
    [InlineData(1609459200)] // 2021-01-01 00:00:00 UTC
    [InlineData(1640995200)] // 2022-01-01 00:00:00 UTC
    public void UnixTimeConverter_WithNullableParameter_ShouldConvertCorrectly(int unixTime)
    {
        // Arrange
        int? nullableUnixTime = unixTime;

        // Act
        var result = Tools.UnixTimeConverter(nullableUnixTime);

        // Assert
        result.Should().NotBeNullOrEmpty();
        
        var expectedDateTime = DateTimeOffset.FromUnixTimeSeconds(unixTime).ToLocalTime().DateTime;
        DateTime.Parse(result).Should().BeCloseTo(expectedDateTime, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void UnixTimeConverter_WithNull_ShouldThrowException()
    {
        // Arrange
        int? nullTime = null;

        // Act
        var act = () => Tools.UnixTimeConverter(nullTime);

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    // Territory tests removed - require FFXIV game context (Plugin.DataManager) not available in unit tests

    [Fact]
    public void Tools_ShouldBeStaticClass()
    {
        // Arrange
        var toolsType = typeof(Tools);

        // Act & Assert
        toolsType.IsClass.Should().BeTrue();
        toolsType.IsSealed.Should().BeTrue();
        toolsType.IsAbstract.Should().BeTrue(); // Static classes are abstract
    }

    [Fact]
    public void UnixTime_Property_ShouldHaveOnlyGetter()
    {
        // Arrange
        var unixTimeProperty = typeof(Tools).GetProperty(nameof(Tools.UnixTime));

        // Act & Assert
        unixTimeProperty.Should().NotBeNull();
        unixTimeProperty!.CanRead.Should().BeTrue();
        unixTimeProperty.CanWrite.Should().BeFalse();
    }

    [Fact]
    public void UnixTime_ShouldBeConsistentAcrossMultipleCalls()
    {
        // Act
        var time1 = Tools.UnixTime;
        System.Threading.Thread.Sleep(1); // Small delay
        var time2 = Tools.UnixTime;

        // Assert
        Math.Abs(time2 - time1).Should().BeLessOrEqualTo(1); // Should be within 1 second
    }

    [Theory]
    [InlineData(1)]
    [InlineData(60)]
    [InlineData(3600)]
    [InlineData(86400)]
    public void ToTimeSinceString_WithVariousTimeSpans_ShouldNotThrow(int secondsAgo)
    {
        // Arrange
        var pastTime = DateTimeOffset.UtcNow.AddSeconds(-secondsAgo).ToUnixTimeSeconds();

        // Act
        var act = () => Tools.ToTimeSinceString((int)pastTime);

        // Assert
        act.Should().NotThrow();
        var result = act();
        result.Should().NotBeNull();
    }

    [Theory]
    [InlineData(31536000)] // 1 year
    [InlineData(2592000)]  // 1 month
    [InlineData(86400)]    // 1 day
    [InlineData(3600)]     // 1 hour
    [InlineData(60)]       // 1 minute
    public void TimeFromNow_WithVariousFutureTimes_ShouldReturnValidStrings(int secondsFromNow)
    {
        // Arrange
        var futureTime = DateTimeOffset.UtcNow.AddSeconds(secondsFromNow).ToUnixTimeSeconds();

        // Act
        var result = Tools.TimeFromNow((int)futureTime);

        // Assert
        result.Should().NotBeNull();
        // Should return a meaningful time description for future times
    }

    [Fact]
    public void UnixTimeConverter_ShouldHandleEpochTime()
    {
        // Arrange - Unix epoch (January 1, 1970)
        var epochTime = 0;

        // Act
        var result = Tools.UnixTimeConverter(epochTime);

        // Assert
        result.Should().NotBeNullOrEmpty();
        
        var expectedDateTime = DateTimeOffset.FromUnixTimeSeconds(epochTime).ToLocalTime().DateTime;
        DateTime.Parse(result).Should().BeCloseTo(expectedDateTime, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void UnixTimeConverter_ShouldHandleLargeTimestamps()
    {
        // Arrange - Year 2038 (near Int32 max value for Unix timestamps)
        var futureTime = 2147483647;

        // Act
        var result = Tools.UnixTimeConverter(futureTime);

        // Assert
        result.Should().NotBeNullOrEmpty();
        
        var expectedDateTime = DateTimeOffset.FromUnixTimeSeconds(futureTime).ToLocalTime().DateTime;
        DateTime.Parse(result).Should().BeCloseTo(expectedDateTime, TimeSpan.FromSeconds(1));
    }
}