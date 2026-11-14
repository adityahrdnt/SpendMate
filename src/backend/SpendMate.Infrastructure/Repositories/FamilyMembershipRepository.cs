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

    public async Task<IReadOnlyList<FamilyMembership>> GetMembersAsync(
        Guid familyId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.FamilyMemberships
            .AsNoTracking()
            .Where(membership => membership.FamilyId == familyId)
            .Include(membership => membership.User)
            .OrderBy(membership => membership.JoinedAtUtc)
            .ThenBy(membership => membership.UserId)
            .ToListAsync(cancellationToken);
    }

    public async Task<FamilyMembershipOperationResult> UpdateRoleAsync(
        Guid familyId,
        Guid requesterUserId,
        Guid targetUserId,
        FamilyRole newRole,
        CancellationToken cancellationToken = default)
    {
        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        var family = await dbContext.Families
            .FromSqlInterpolated(
                $"SELECT * FROM \"Families\" WHERE \"Id\" = {familyId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

        if (family is null)
        {
            return FamilyMembershipOperationResult.FamilyNotFound;
        }

        if (family.Type != FamilyType.Shared)
        {
            return FamilyMembershipOperationResult.NotSharedFamily;
        }

        var requesterMembership = await dbContext.FamilyMemberships
            .SingleOrDefaultAsync(
                x => x.FamilyId == familyId &&
                    x.UserId == requesterUserId,
                cancellationToken);

        if (requesterMembership?.Role != FamilyRole.Owner)
        {
            return FamilyMembershipOperationResult.RequesterNotOwner;
        }

        if (requesterUserId == targetUserId)
        {
            return FamilyMembershipOperationResult.CannotChangeOwnRole;
        }

        var targetMembership = await dbContext.FamilyMemberships
            .SingleOrDefaultAsync(
                x => x.FamilyId == familyId &&
                    x.UserId == targetUserId,
                cancellationToken);

        if (targetMembership is null)
        {
            return FamilyMembershipOperationResult.TargetMembershipNotFound;
        }

        if (targetMembership.Role == newRole)
        {
            await transaction.CommitAsync(cancellationToken);
            return FamilyMembershipOperationResult.Success;
        }

        if (targetMembership.Role == FamilyRole.Owner &&
            newRole == FamilyRole.Member)
        {
            var ownerCount = await dbContext.FamilyMemberships
                .CountAsync(
                    x => x.FamilyId == familyId &&
                        x.Role == FamilyRole.Owner,
                    cancellationToken);

            if (ownerCount <= 1)
            {
                return FamilyMembershipOperationResult.LastOwner;
            }
        }

        targetMembership.Role = newRole;

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return FamilyMembershipOperationResult.Success;
    }


    public async Task<FamilyMembershipOperationResult> RemoveMemberAsync(
        Guid familyId,
        Guid requesterUserId,
        Guid targetUserId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        var family = await dbContext.Families
            .FromSqlInterpolated(
                $"SELECT * FROM \"Families\" WHERE \"Id\" = {familyId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

        if (family is null)
        {
            return FamilyMembershipOperationResult.FamilyNotFound;
        }

        if (family.Type != FamilyType.Shared)
        {
            return FamilyMembershipOperationResult.NotSharedFamily;
        }

        var requesterMembership = await dbContext.FamilyMemberships
            .SingleOrDefaultAsync(
                x => x.FamilyId == familyId &&
                     x.UserId == requesterUserId,
                cancellationToken);

        if (requesterMembership?.Role != FamilyRole.Owner)
        {
            return FamilyMembershipOperationResult.RequesterNotOwner;
        }

        var targetMembership = await dbContext.FamilyMemberships
            .SingleOrDefaultAsync(
                x => x.FamilyId == familyId &&
                     x.UserId == targetUserId,
                cancellationToken);

        if (targetMembership is null)
        {
            return FamilyMembershipOperationResult.TargetMembershipNotFound;
        }

        if (targetMembership.Role == FamilyRole.Owner)
        {
            var ownerCount = await dbContext.FamilyMemberships
                .CountAsync(
                    x => x.FamilyId == familyId &&
                         x.Role == FamilyRole.Owner,
                    cancellationToken);

            if (ownerCount <= 1)
            {
                return FamilyMembershipOperationResult.LastOwner;
            }
        }

        var targetUser = await dbContext.Users
            .FromSqlInterpolated(
                $"SELECT * FROM \"Users\" WHERE \"Id\" = {targetUserId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

        if (targetUser is null)
        {
            return FamilyMembershipOperationResult.TargetMembershipNotFound;
        }

        if (targetUser.ActiveFamilyId == familyId)
        {
            var personalFamily = await dbContext.Families
                .SingleOrDefaultAsync(
                    x => x.Type == FamilyType.Personal &&
                         x.PersonalOwnerUserId == targetUserId,
                    cancellationToken);

            if (personalFamily is null)
            {
                return FamilyMembershipOperationResult.PersonalFamilyNotFound;
            }

            targetUser.ActiveFamilyId = personalFamily.Id;
        }

        dbContext.FamilyMemberships.Remove(targetMembership);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return FamilyMembershipOperationResult.Success;
    }


    public async Task<ActiveFamilyOperationResult> SetActiveFamilyAsync(
        Guid userId,
        Guid familyId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        var family = await dbContext.Families
            .FromSqlInterpolated(
                $"SELECT * FROM \"Families\" WHERE \"Id\" = {familyId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

        if (family is null)
        {
            return ActiveFamilyOperationResult.FamilyNotFound;
        }

        var user = await dbContext.Users
            .FromSqlInterpolated(
                $"SELECT * FROM \"Users\" WHERE \"Id\" = {userId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            return ActiveFamilyOperationResult.MembershipNotFound;
        }

        var isMember = await dbContext.FamilyMemberships
            .AnyAsync(
                x => x.FamilyId == familyId &&
                     x.UserId == userId,
                cancellationToken);

        if (!isMember)
        {
            return ActiveFamilyOperationResult.MembershipNotFound;
        }

        user.ActiveFamilyId = familyId;

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ActiveFamilyOperationResult.Success;
    }

    public Task<bool> IsSharedFamilyMemberAsync(
        Guid familyId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.FamilyMemberships
            .AnyAsync(
                membership =>
                    membership.FamilyId == familyId &&
                    membership.UserId == userId &&
                    membership.Family.Type == FamilyType.Shared,
                cancellationToken);
    }
}
