using AlphaScopeServer.Services.Lodestone;

namespace AlphaScopeServer.Tests.TestDoubles;

/// <summary>
/// Test double for ILodestoneBioFetcher. Takes a dictionary of lodestoneId → canned bio
/// (or null = Success but bio=null). Any lodestoneId not in the dictionary returns
/// Success=false with reason "not found".
/// </summary>
public sealed class FakeLodestoneBioFetcher : ILodestoneBioFetcher
{
    private readonly Dictionary<int, string?> _bios;
    private readonly bool _simulateFailure;
    private readonly string? _failureReason;

    public FakeLodestoneBioFetcher(Dictionary<int, string?> bios)
    {
        _bios = bios;
        _simulateFailure = false;
    }

    private FakeLodestoneBioFetcher(string failureReason)
    {
        _bios = new();
        _simulateFailure = true;
        _failureReason = failureReason;
    }

    public static FakeLodestoneBioFetcher AlwaysFailsWith(string reason)
        => new(reason);

    public Task<BioFetchResult> FetchBioAsync(int lodestoneId, CancellationToken ct)
    {
        if (_simulateFailure)
            return Task.FromResult(new BioFetchResult(false, null, _failureReason));

        if (_bios.TryGetValue(lodestoneId, out var bio))
            return Task.FromResult(new BioFetchResult(true, bio, null));

        return Task.FromResult(new BioFetchResult(false, null, "not found"));
    }
}
