using Microsoft.EntityFrameworkCore;
using SpendMate.Application.Abstractions;
using SpendMate.Infrastructure.Data;

namespace SpendMate.Infrastructure.Repositories;

public sealed class FamilyMembershipRepository(
    SpendMateDbContext dbContext) : IFamilyMembershipRepository
{
    public Task<bool> IsMemberAsync(
        Guid familyId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.FamilyMemberships
            .AnyAsync(
                membership =>
                    membership.FamilyId == familyId &&
                    membership.UserId == userId,
                cancellationToken);
    }

    public Task<bool> IsMemberByEmailAsync(
    Guid familyId,
    string normalizedEmail,
    CancellationToken cancellationToken = default)
    {
        return dbContext.FamilyMemberships
            .AnyAsync(
                membership =>
                    membership.FamilyId == familyId &&
                    membership.User.Email == normalizedEmail,
                cancellationToken);
    }
}
