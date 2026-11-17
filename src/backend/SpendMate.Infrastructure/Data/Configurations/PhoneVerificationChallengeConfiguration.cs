using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SpendMate.Domain.Entities;

namespace SpendMate.Infrastructure.Data.Configurations;

public class PhoneVerificationChallengeConfiguration
    : IEntityTypeConfiguration<PhoneVerificationChallenge>
{
    public void Configure(
        EntityTypeBuilder<PhoneVerificationChallenge> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.CodeHash)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.PhoneNumber)
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(x => x.CodeCiphertext)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.EncryptionNonce)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.EncryptionTag)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.Property(x => x.ExpiresAtUtc)
            .IsRequired();

        builder.Property(x => x.LastSentAtUtc)
            .IsRequired();

        builder.Property(x => x.ConsumedAtUtc)
            .IsRequired(false);

        builder.Property(x => x.Attempts)
            .IsRequired();

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.UserId)
            .IsUnique()
            .HasFilter("\"ConsumedAtUtc\" IS NULL");

        builder.HasIndex(x => x.ExpiresAtUtc);
    }
}
