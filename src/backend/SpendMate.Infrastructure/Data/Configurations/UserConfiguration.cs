using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SpendMate.Domain.Entities;

namespace SpendMate.Infrastructure.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Email)
            .HasMaxLength(254)
            .IsRequired();

        builder.Property(x => x.DisplayName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.PasswordHash)
            .HasMaxLength(500)
            .IsRequired(false);

        builder.HasOne<Family>()
            .WithMany()
            .HasForeignKey(x => x.ActiveFamilyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.ActiveFamilyId);

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.Property(x => x.EmailVerifiedAtUtc)
            .IsRequired(false);

        builder.Property(x => x.EmailVerificationTokenHash)
            .HasMaxLength(64)
            .IsRequired(false);

        builder.Property(x => x.EmailVerificationExpiresAtUtc)
            .IsRequired(false);

        builder.HasIndex(x => x.EmailVerificationTokenHash)
            .IsUnique()
            .HasFilter("\"EmailVerificationTokenHash\" IS NOT NULL");

        builder.HasIndex(x => x.Email)
            .IsUnique();
    }
}
