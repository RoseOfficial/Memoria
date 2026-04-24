using System.Collections.Concurrent;

namespace AlphaScopeServer.Services.Takedowns;

public class TakedownRateLimiter
{
    private readonly ConcurrentDictionary<string, List<DateTime>> _hits = new();
    private const int Limit = 3;
    private static readonly TimeSpan Window = TimeSpan.FromHours(1);

    public bool Allow(string ipHash)
    {
        var now = DateTime.UtcNow;
        var list = _hits.GetOrAdd(ipHash, _ => new List<DateTime>());
        lock (list)
        {
            list.RemoveAll(t => now - t > Window);
            if (list.Count >= Limit) return false;
            list.Add(now);
            return true;
        }
    }

    public static string HashIp(string ip)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(ip));
        return Convert.ToHexString(bytes).Substring(0, 32);
    }
}
