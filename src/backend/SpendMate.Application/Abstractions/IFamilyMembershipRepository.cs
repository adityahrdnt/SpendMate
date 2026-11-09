using SpendMate.Domain.Entities;

namespace SpendMate.Application.Abstractions;

public interface IFamilyMembershipRepository
{
    Task<bool> IsMemberAsync(
        Guid familyId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<bool> IsMemberByEmailAsync(
        Guid familyId,
        string normalizedEmail,
        CancellationToken cancellationToken = default);

    Task<bool> AddWithInvitationAsync(
        FamilyMembership membership,
        FamilyInvitation invitation,
        DateTimeOffset acceptedAtUtc,
        CancellationToken cancellationToken = default);
}
