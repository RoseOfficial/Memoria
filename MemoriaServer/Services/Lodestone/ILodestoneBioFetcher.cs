namespace MemoriaServer.Services.Lodestone
{
    /// <summary>
    /// Fetches the Bio text for a given Lodestone character id. Used by the claim verification
    /// endpoint to check whether a user's verification code appears in the target character's
    /// Lodestone bio. Injected via interface so tests can swap a fake with canned responses.
    /// </summary>
    public interface ILodestoneBioFetcher
    {
        Task<BioFetchResult> FetchBioAsync(int lodestoneId, CancellationToken ct);
    }

    /// <summary>
    /// Result of a single bio fetch. Success=true means Bio is populated (may be empty string
    /// for a character with no bio set). Success=false means the fetch failed; ErrorReason is
    /// a short human-readable hint used in 503 responses.
    /// </summary>
    public record BioFetchResult(bool Success, string? Bio, string? ErrorReason);
}
