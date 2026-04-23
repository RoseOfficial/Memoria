namespace AlphaScopeServer.Models.DTOs
{
    public sealed record LinkGenerateResponse(
        string Code,
        DateTime ExpiresAt);
}
