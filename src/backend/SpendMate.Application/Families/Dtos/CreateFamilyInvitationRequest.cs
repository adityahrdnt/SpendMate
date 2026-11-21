using SpendMate.Domain.Enums;

namespace SpendMate.Application.Families.Dtos;

public sealed record CreateFamilyInvitationRequest(
    FamilyInvitationType InvitationType = FamilyInvitationType.Email,
    string? Email = null,
    string? PhoneNumber = null);
