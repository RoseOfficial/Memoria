using NSubstitute;
using System.Reflection;

namespace TestUtilities;

public static class TestHelpers
{
    public static void SetPrivateField<T>(object obj, string fieldName, T value)
    {
        var field = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        if (field == null)
        {
            throw new ArgumentException($"Field '{fieldName}' not found on type '{obj.GetType()}'");
        }
        field.SetValue(obj, value);
    }

    public static T GetPrivateField<T>(object obj, string fieldName)
    {
        var field = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        if (field == null)
        {
            throw new ArgumentException($"Field '{fieldName}' not found on type '{obj.GetType()}'");
        }
        return (T)field.GetValue(obj);
    }

    public static async Task<T> CallPrivateMethod<T>(object obj, string methodName, params object[] parameters)
    {
        var method = obj.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
        if (method == null)
        {
            throw new ArgumentException($"Method '{methodName}' not found on type '{obj.GetType()}'");
        }

        var result = method.Invoke(obj, parameters);
        
        if (result is Task<T> taskResult)
        {
            return await taskResult;
        }
        
        if (result is Task task)
        {
            await task;
            return default(T);
        }

        return (T)result;
    }

    public static string GenerateRandomString(int length = 10)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, length)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }

    public static uint GenerateRandomContentId()
    {
        var random = new Random();
        return (uint)random.Next(10000000, 99999999);
    }

    public static ulong GenerateRandomRetainerId()
    {
        var random = new Random();
        return (ulong)random.NextInt64(10000000000, 99999999999);
    }

    public static async Task WaitForConditionAsync(Func<bool> condition, TimeSpan timeout, TimeSpan interval = default)
    {
        if (interval == default)
            interval = TimeSpan.FromMilliseconds(50);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        while (stopwatch.Elapsed < timeout)
        {
            if (condition())
                return;
                
            await Task.Delay(interval);
        }
        
        throw new TimeoutException($"Condition was not met within {timeout}");
    }

    public static void SimulateAsyncDelay(TimeSpan delay)
    {
        Task.Delay(delay).Wait();
    }
}