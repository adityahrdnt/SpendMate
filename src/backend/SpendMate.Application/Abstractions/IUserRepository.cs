using SpendMate.Domain.Entities;

namespace SpendMate.Application.Abstractions;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<User?> GetByIdWithMembershipsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<User?> GetByEmailVerificationTokenHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default);

    Task<User?> GetByEmailAsync(
        string email,
        CancellationToken cancellationToken = default);

    Task AddWithActiveFamilyAsync(
        User user,
        Guid activeFamilyId,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(
        CancellationToken cancellationToken = default);
}
