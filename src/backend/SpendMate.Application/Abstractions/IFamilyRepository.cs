using SpendMate.Domain.Entities;

namespace SpendMate.Application.Abstractions;

public interface IFamilyRepository
{
    Task<IReadOnlyList<FamilyMembership>> GetMembershipsByUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<Family> CreateWithOwnerAsync(
        Family family,
        FamilyMembership ownerMembership,
        CancellationToken cancellationToken = default);
}
