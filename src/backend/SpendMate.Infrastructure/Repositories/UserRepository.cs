using Microsoft.EntityFrameworkCore;
using SpendMate.Application.Abstractions;
using SpendMate.Domain.Entities;
using SpendMate.Infrastructure.Data;

namespace SpendMate.Infrastructure.Repositories;

public sealed class UserRepository(
    SpendMateDbContext dbContext) : IUserRepository
{
    public Task<User?> GetByIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Users
            .SingleOrDefaultAsync(
                user => user.Id == userId,
                cancellationToken);
    }

    public Task<User?> GetByIdWithMembershipsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Users
            .Include(user => user.Memberships)
                .ThenInclude(membership => membership.Family)
            .SingleOrDefaultAsync(
                user => user.Id == userId,
                cancellationToken);
    }

    public Task<User?> GetByEmailVerificationTokenHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Users
            .SingleOrDefaultAsync(
                user => user.EmailVerificationTokenHash == tokenHash,
                cancellationToken);
    }

    public Task<User?> GetByEmailAsync(
    string email,
    CancellationToken cancellationToken = default)
    {
        return dbContext.Users
            .Include(user => user.Memberships)
                .ThenInclude(membership => membership.Family)
            .SingleOrDefaultAsync(
                user => user.Email == email,
                cancellationToken);
    }

    public async Task AddWithActiveFamilyAsync(
        User user,
        Guid activeFamilyId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        dbContext.Users.Add(user);

        // Save User, Personal Family, and Membership first.
        await dbContext.SaveChangesAsync(cancellationToken);

        // Set Active Family after the Family exists in the database.
        user.ActiveFamilyId = activeFamilyId;

        await dbContext.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    public Task SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}
