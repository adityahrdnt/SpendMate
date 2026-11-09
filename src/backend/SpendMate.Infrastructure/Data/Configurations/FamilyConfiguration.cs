using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SpendMate.Domain.Entities;
using SpendMate.Domain.Enums;

namespace SpendMate.Infrastructure.Data.Configurations;

public class FamilyConfiguration : IEntityTypeConfiguration<Family>
{
    public void Configure(EntityTypeBuilder<Family> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.Property(x => x.Type)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.PersonalOwnerUserId)
            .IsRequired(false);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.PersonalOwnerUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.PersonalOwnerUserId)
            .IsUnique()
            .HasFilter("\"PersonalOwnerUserId\" IS NOT NULL");

        builder.ToTable(table => table.HasCheckConstraint(
            "CK_Families_Type_PersonalOwner",
            """
            ("Type" = 'Personal' AND "PersonalOwnerUserId" IS NOT NULL)
            OR
            ("Type" = 'Shared' AND "PersonalOwnerUserId" IS NULL)
            """));
    }
}
