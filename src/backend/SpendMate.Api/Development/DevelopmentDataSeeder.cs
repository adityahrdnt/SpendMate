using Microsoft.EntityFrameworkCore;
using SpendMate.Domain.Entities;
using SpendMate.Domain.Enums;
using SpendMate.Infrastructure.Data;

namespace SpendMate.Api.Development;

public static class DevelopmentDataSeeder
{
    public static async Task SeedAsync(
        SpendMateDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users
            .SingleOrDefaultAsync(
                x => x.Id == DevelopmentIds.UserId,
                cancellationToken);

        if (user is null)
        {
            var emailExists = await dbContext.Users
                .AnyAsync(
                    x => x.Email == "developer@spendmate.local",
                    cancellationToken);

            if (emailExists)
            {
                throw new InvalidOperationException(
                    "The Development seed email already belongs to another user.");
            }

            user = new User
            {
                Id = DevelopmentIds.UserId,
                Email = "developer@spendmate.local",
                DisplayName = "SpendMate Developer"
            };

            dbContext.Users.Add(user);
        }

        var family = await dbContext.Families
            .SingleOrDefaultAsync(
                x => x.Id == DevelopmentIds.FamilyId,
                cancellationToken);

        if (family is null)
        {
            family = new Family
            {
                Id = DevelopmentIds.FamilyId,
                Name = "SpendMate Demo Family",
                Type = FamilyType.Personal,
                PersonalOwnerUserId = DevelopmentIds.UserId
            };

            dbContext.Families.Add(family);
        }
        else
        {
            family.Type = FamilyType.Personal;
            family.PersonalOwnerUserId = DevelopmentIds.UserId;
        }

        var membershipExists = await dbContext.FamilyMemberships
            .AnyAsync(
                x => x.FamilyId == DevelopmentIds.FamilyId &&
                     x.UserId == DevelopmentIds.UserId,
                cancellationToken);

        if (!membershipExists)
        {
            dbContext.FamilyMemberships.Add(new FamilyMembership
            {
                FamilyId = DevelopmentIds.FamilyId,
                UserId = DevelopmentIds.UserId,
                Role = FamilyRole.Owner
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
