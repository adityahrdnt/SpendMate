
using Microsoft.EntityFrameworkCore;
using SpendMate.Application.Abstractions;
using SpendMate.Domain.Entities;
using SpendMate.Infrastructure.Data;

namespace SpendMate.Infrastructure.PhoneVerification;

public sealed class PhoneVerificationTransaction(
    SpendMateDbContext dbContext)
    : IPhoneVerificationTransaction
{
    public async Task<TResult> ExecuteAsync<TResult>(
        Guid userId,
        Func<
            User?,
            PhoneVerificationChallenge?,
            CancellationToken,
            Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        try
        {
            var user = await dbContext.Users
                .FromSqlInterpolated(
                    $"SELECT * FROM \"Users\" WHERE \"Id\" = {userId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);

            PhoneVerificationChallenge? challenge = null;

            if (user is not null)
            {
                challenge = await dbContext.PhoneVerificationChallenges
                    .SingleOrDefaultAsync(
                        x => x.UserId == userId &&
                             x.ConsumedAtUtc == null,
                        cancellationToken);
            }

            var result = await operation(
                user,
                challenge,
                cancellationToken);

            await dbContext.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return result;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
