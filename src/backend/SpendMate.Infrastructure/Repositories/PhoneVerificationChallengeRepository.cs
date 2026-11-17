using Microsoft.EntityFrameworkCore;
using SpendMate.Application.Abstractions;
using SpendMate.Domain.Entities;
using SpendMate.Infrastructure.Data;

namespace SpendMate.Infrastructure.Repositories;

public sealed class PhoneVerificationChallengeRepository(
    SpendMateDbContext dbContext)
    : IPhoneVerificationChallengeRepository
{
    public Task<PhoneVerificationChallenge?> GetActiveByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.PhoneVerificationChallenges
            .SingleOrDefaultAsync(
                challenge =>
                    challenge.UserId == userId &&
                    challenge.ConsumedAtUtc == null,
                cancellationToken);
    }

    public async Task AddAsync(
        PhoneVerificationChallenge challenge,
        CancellationToken cancellationToken = default)
    {
        await dbContext.PhoneVerificationChallenges.AddAsync(
            challenge,
            cancellationToken);
    }

    public Task SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}
