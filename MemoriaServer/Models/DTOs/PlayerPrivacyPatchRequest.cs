namespace MemoriaServer.Models.DTOs;

public record PlayerPrivacyPatchRequest(
    bool? HideAlts,
    bool? HideEncounters,
    bool? HideEntirely);
