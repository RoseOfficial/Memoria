namespace AlphaScopeServer.Models.DTOs
{
    public sealed record ClaimVerifyResponse(
        bool Claimed,
        string? CharacterName = null,
        short? HomeWorldId = null,
        int? AttemptsLeft = null);
}
