using Microsoft.EntityFrameworkCore;
using SpendMate.Application.Abstractions;
using SpendMate.Domain.Entities;
using SpendMate.Infrastructure.Data;

namespace SpendMate.Infrastructure.Repositories;

public sealed class FamilyRepository : IFamilyRepository
{
    private readonly SpendMateDbContext _dbContext;

    public FamilyRepository(SpendMateDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<FamilyMembership>> GetMembershipsByUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.FamilyMemberships
            .AsNoTracking()
            .Include(membership => membership.Family)
            .Where(membership => membership.UserId == userId)
            .OrderBy(membership => membership.JoinedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<Family> CreateWithOwnerAsync(
        Family family,
        FamilyMembership ownerMembership,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.Families.AddAsync(family, cancellationToken);
        await _dbContext.FamilyMemberships.AddAsync(ownerMembership, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return family;
    }
}
