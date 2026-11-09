using Microsoft.EntityFrameworkCore;
using SpendMate.Application.Abstractions;
using SpendMate.Domain.Entities;
using SpendMate.Domain.Enums;
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

    public async Task<bool> AddWithInvitationAsync(
        FamilyMembership membership,
        FamilyInvitation invitation,
        DateTimeOffset acceptedAtUtc,
        CancellationToken cancellationToken = default)
    {
        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        var updatedRows = await dbContext.FamilyInvitations
            .Where(x =>
                x.Id == invitation.Id &&
                x.Status == FamilyInvitationStatus.Pending &&
                x.ExpiresAtUtc > acceptedAtUtc)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(
                        x => x.Status,
                        FamilyInvitationStatus.Accepted)
                    .SetProperty(
                        x => x.AcceptedAtUtc,
                        acceptedAtUtc)
                    .SetProperty(
                        x => x.AcceptedByUserId,
                        membership.UserId),
                cancellationToken);

        if (updatedRows == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        await dbContext.FamilyMemberships.AddAsync(
            membership,
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return true;
    }
}
