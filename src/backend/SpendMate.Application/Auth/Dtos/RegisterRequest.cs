namespace SpendMate.Application.Auth.Dtos;

public sealed record RegisterRequest(
    string Email,
    string DisplayName,
    string Password,
    string FamilyName);
