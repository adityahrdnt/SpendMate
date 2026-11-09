namespace SpendMate.Application.Auth.Dtos;

public sealed record AuthResponse(
    Guid UserId,
    string Email,
    string DisplayName,
    Guid FamilyId,
    string FamilyName);
