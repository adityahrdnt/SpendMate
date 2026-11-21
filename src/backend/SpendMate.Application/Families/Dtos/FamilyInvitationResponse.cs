using SpendMate.Domain.Enums;

namespace SpendMate.Application.Families.Dtos;

public sealed record FamilyInvitationResponse(
    Guid Id,
    Guid FamilyId,
    FamilyInvitationType InvitationType,
    string? Email,
    string? PhoneNumber,
    string Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset ExpiresAtUtc);
