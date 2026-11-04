using Microsoft.EntityFrameworkCore;
using SpendMate.Application.Abstractions;
using SpendMate.Domain.Entities;
using SpendMate.Infrastructure.Data;

namespace SpendMate.Infrastructure.Repositories;

public sealed class UserRepository(
    SpendMateDbContext dbContext) : IUserRepository
{
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
}
