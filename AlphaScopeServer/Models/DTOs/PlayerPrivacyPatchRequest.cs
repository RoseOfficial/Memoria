namespace AlphaScopeServer.Models.DTOs;

public record PlayerPrivacyPatchRequest(
    bool? HideAlts,
    bool? HideEncounters,
    bool? HideEntirely);
