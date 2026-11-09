namespace SpendMate.Application.Families.Dtos;

public sealed record CreateFamilyInvitationResult(
    FamilyInvitationResponse Invitation,
    string Token,
    bool WasResent);
