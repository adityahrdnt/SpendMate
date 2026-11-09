namespace SpendMate.Application.Families.Dtos;

public sealed record FamilyInvitationResponse(
    Guid Id,
    Guid FamilyId,
    string Email,
    string Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset ExpiresAtUtc);
