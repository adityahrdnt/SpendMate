using Microsoft.EntityFrameworkCore;
using SpendMate.Application.Abstractions;
using SpendMate.Domain.Entities;
using SpendMate.Domain.Enums;
using SpendMate.Infrastructure.Data;

namespace SpendMate.Infrastructure.Repositories;

public sealed class FamilyInvitationRepository(
    SpendMateDbContext dbContext) : IFamilyInvitationRepository
{
    public Task<FamilyInvitation?> GetByTokenHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default)
    {
        return dbContext.FamilyInvitations
            .SingleOrDefaultAsync(
                invitation => invitation.TokenHash == tokenHash,
                cancellationToken);
    }

    public Task<FamilyInvitation?> GetByIdAndFamilyAsync(
        Guid invitationId,
        Guid familyId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.FamilyInvitations
            .SingleOrDefaultAsync(
                invitation =>
                    invitation.Id == invitationId &&
                    invitation.FamilyId == familyId,
                cancellationToken);
    }

    public Task<FamilyInvitation?> GetPendingByFamilyAndEmailAsync(
        Guid familyId,
        string normalizedEmail,
        CancellationToken cancellationToken = default)
    {
        return dbContext.FamilyInvitations
            .Where(invitation =>
                invitation.FamilyId == familyId &&
                invitation.NormalizedEmail == normalizedEmail &&
                invitation.Status == FamilyInvitationStatus.Pending)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task AddAsync(
        FamilyInvitation invitation,
        CancellationToken cancellationToken = default)
    {
        await dbContext.FamilyInvitations.AddAsync(
            invitation,
            cancellationToken);
    }

    public Task SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}
