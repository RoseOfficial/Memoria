namespace AlphaScopeServer.Models.DTOs
{
    public sealed record ClaimStartResponse(
        string Code,
        DateTime ExpiresAt,
        string Instructions);
}
