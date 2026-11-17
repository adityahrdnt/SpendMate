using SpendMate.Domain.Entities;

namespace SpendMate.Application.Abstractions;

public interface IPhoneVerificationChallengeRepository
{
    Task<PhoneVerificationChallenge?> GetActiveByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        PhoneVerificationChallenge challenge,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(
        CancellationToken cancellationToken = default);
}
