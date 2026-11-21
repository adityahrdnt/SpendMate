using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SpendMate.Domain.Entities;

namespace SpendMate.Infrastructure.Data.Configurations;

public class FamilyInvitationConfiguration
    : IEntityTypeConfiguration<FamilyInvitation>
{
    public void Configure(EntityTypeBuilder<FamilyInvitation> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.InvitationType)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.Email)
            .HasMaxLength(254)
            .IsRequired(false);

        builder.Property(x => x.NormalizedEmail)
            .HasMaxLength(254)
            .IsRequired(false);

        builder.Property(x => x.PhoneNumber)
            .HasMaxLength(16)
            .IsRequired(false);

        builder.Property(x => x.TokenHash)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.ExpiresAtUtc).IsRequired();
        builder.Property(x => x.AcceptedAtUtc).IsRequired(false);
        builder.Property(x => x.RevokedAtUtc).IsRequired(false);

        builder.HasOne(x => x.Family)
            .WithMany()
            .HasForeignKey(x => x.FamilyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.InvitedByUser)
            .WithMany()
            .HasForeignKey(x => x.InvitedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.AcceptedByUser)
            .WithMany()
            .HasForeignKey(x => x.AcceptedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.TokenHash)
            .IsUnique();

        builder.HasIndex(x => new
        {
            x.FamilyId,
            x.NormalizedEmail
        })
            .IsUnique()
            .HasFilter(
                "\"Status\" = 'Pending' AND \"InvitationType\" = 'Email'");

        builder.HasIndex(x => new
        {
            x.FamilyId,
            x.PhoneNumber
        })
            .IsUnique()
            .HasFilter(
                "\"Status\" = 'Pending' AND \"InvitationType\" = 'Phone'");

        builder.ToTable(table => table.HasCheckConstraint(
            "CK_FamilyInvitations_Recipient",
            """
            (
                "InvitationType" = 'Email'
                AND "Email" IS NOT NULL
                AND "NormalizedEmail" IS NOT NULL
                AND "PhoneNumber" IS NULL
            )
            OR
            (
                "InvitationType" = 'Phone'
                AND "Email" IS NULL
                AND "NormalizedEmail" IS NULL
                AND "PhoneNumber" IS NOT NULL
            )
            """));
    }
}
