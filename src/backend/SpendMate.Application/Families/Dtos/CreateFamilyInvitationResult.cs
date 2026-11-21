namespace SpendMate.Application.Families.Dtos;

public sealed record CreateFamilyInvitationResult(
    FamilyInvitationResponse Invitation,
    string InvitationLink,
    bool WasResent);
