using SpendMate.Domain.Entities;

namespace SpendMate.Application.Abstractions;

public interface IFamilyInvitationRepository
{
    Task<FamilyInvitation?> GetByIdAndFamilyAsync(
        Guid invitationId,
        Guid familyId,
        CancellationToken cancellationToken = default);

    Task<FamilyInvitation?> GetPendingByFamilyAndEmailAsync(
        Guid familyId,
        string normalizedEmail,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        FamilyInvitation invitation,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(
        CancellationToken cancellationToken = default);
}
