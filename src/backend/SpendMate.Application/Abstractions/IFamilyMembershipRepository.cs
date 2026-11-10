using SpendMate.Domain.Entities;
using SpendMate.Domain.Enums;

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

    Task<IReadOnlyList<FamilyMembership>> GetMembersAsync(
        Guid familyId,
        CancellationToken cancellationToken = default);

    Task<FamilyMembershipOperationResult> UpdateRoleAsync(
        Guid familyId,
        Guid requesterUserId,
        Guid targetUserId,
        FamilyRole newRole,
        CancellationToken cancellationToken = default);

    Task<FamilyMembershipOperationResult> RemoveMemberAsync(
        Guid familyId,
        Guid requesterUserId,
        Guid targetUserId,
        CancellationToken cancellationToken = default);

    Task<bool> IsSharedFamilyMemberAsync(
        Guid familyId,
        Guid userId,
        CancellationToken cancellationToken = default);
}
