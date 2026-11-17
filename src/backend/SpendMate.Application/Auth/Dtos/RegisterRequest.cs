namespace SpendMate.Application.Auth.Dtos;

public sealed record RegisterRequest(
    string Email,
    string PhoneNumber,
    string DisplayName,
    string Password,
    string FamilyName);
