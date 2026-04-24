namespace MemoriaServer.Models.DTOs;

public record TakedownListItem(
    int Id,
    string WorldSlug,
    string NameSlug,
    long? ResolvedPlayerLocalContentId,
    string Reason,
    string ContactEmail,
    DateTime SubmittedAt,
    string Status);
