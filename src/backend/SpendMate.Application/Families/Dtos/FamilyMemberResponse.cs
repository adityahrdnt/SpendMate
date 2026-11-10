namespace SpendMate.Application.Families.Dtos;

public sealed record FamilyMemberResponse(
    Guid UserId,
    string DisplayName,
    string Email,
    string Role,
    DateTimeOffset JoinedAtUtc)
{
    public static FamilyMemberResponse FromEntity(
        Domain.Entities.FamilyMembership membership) =>
        new(
            membership.UserId,
            membership.User.DisplayName,
            membership.User.Email,
            membership.Role.ToString(),
            membership.JoinedAtUtc);
}
