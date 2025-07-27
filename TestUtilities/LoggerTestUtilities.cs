using Microsoft.Extensions.Logging;
using NSubstitute;

namespace TestUtilities;

public static class LoggerTestUtilities
{
    public static ILogger<T> CreateMockLogger<T>()
    {
        return Substitute.For<ILogger<T>>();
    }

    public static void VerifyLogCalled<T>(ILogger<T> logger, LogLevel logLevel, string message)
    {
        logger.Received().Log(
            logLevel,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains(message)),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    public static void VerifyLogCalled<T>(ILogger<T> logger, LogLevel logLevel, Times? times = null)
    {
        var expectedTimes = times?.Value ?? 1;
        
        logger.Received(expectedTimes).Log(
            logLevel,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    public static void VerifyNoLogsCalled<T>(ILogger<T> logger)
    {
        logger.DidNotReceive().Log(
            Arg.Any<LogLevel>(),
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }
}

public class Times
{
    public int Value { get; }

    private Times(int value)
    {
        Value = value;
    }

    public static Times Once() => new(1);
    public static Times Twice() => new(2);
    public static Times Exactly(int count) => new(count);
    public static Times Never() => new(0);
}