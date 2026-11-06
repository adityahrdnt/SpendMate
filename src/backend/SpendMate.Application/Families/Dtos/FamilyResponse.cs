using SpendMate.Domain.Entities;

namespace SpendMate.Application.Families.Dtos;

public sealed record FamilyResponse(
    Guid Id,
    string Name,
    string Type,
    string Role,
    DateTimeOffset JoinedAtUtc)
{
    public static FamilyResponse FromEntity(
        FamilyMembership membership) =>
        new(
            membership.Family.Id,
            membership.Family.Name,
            membership.Family.Type.ToString(),
            membership.Role.ToString(),
            membership.JoinedAtUtc);
}
