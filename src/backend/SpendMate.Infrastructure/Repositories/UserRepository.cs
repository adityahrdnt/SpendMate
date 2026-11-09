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

    public async Task AddAsync(
        User user,
        CancellationToken cancellationToken = default)
    {
        dbContext.Users.Add(user);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}
