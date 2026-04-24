namespace MemoriaServer.Models.DTOs;

public record TakedownActionRequest(
    string Action,  // "approve" or "reject"
    string? Notes);
