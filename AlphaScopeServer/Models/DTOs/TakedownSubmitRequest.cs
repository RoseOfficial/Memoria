using System.ComponentModel.DataAnnotations;

namespace AlphaScopeServer.Models.DTOs;

public record TakedownSubmitRequest(
    [Required][MaxLength(64)] string WorldSlug,
    [Required][MaxLength(64)] string NameSlug,
    [Required][MaxLength(1000)] string Reason,
    [Required][MaxLength(320)][EmailAddress] string ContactEmail);
