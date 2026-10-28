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
}
