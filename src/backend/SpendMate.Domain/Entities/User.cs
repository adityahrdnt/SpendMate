namespace SpendMate.Domain.Entities;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? ActiveFamilyId { get; set; }

    public required string Email { get; set; }

    public required string DisplayName { get; set; }

    public string? PasswordHash { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } =
        DateTimeOffset.UtcNow;

    public DateTimeOffset? EmailVerifiedAtUtc { get; set; }

    public string? EmailVerificationTokenHash { get; set; }

    public DateTimeOffset? EmailVerificationExpiresAtUtc { get; set; }

    public ICollection<FamilyMembership> Memberships { get; set; } =
        new List<FamilyMembership>();

    public ICollection<Expense> CreatedExpenses { get; set; } =
        new List<Expense>();
}
