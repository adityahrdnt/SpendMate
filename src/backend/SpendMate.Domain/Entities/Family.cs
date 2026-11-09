using SpendMate.Domain.Enums;

namespace SpendMate.Domain.Entities;

public class Family
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public required string Name { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } =
        DateTimeOffset.UtcNow;

    public ICollection<FamilyMembership> Memberships { get; set; } =
        new List<FamilyMembership>();

    public ICollection<Expense> Expenses { get; set; } =
        new List<Expense>();

    public FamilyType Type { get; set; } = FamilyType.Personal;
    public Guid? PersonalOwnerUserId { get; set; }
}
