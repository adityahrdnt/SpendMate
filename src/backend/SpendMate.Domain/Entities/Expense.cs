
namespace SpendMate.Domain.Entities;

public class Expense
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FamilyId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public decimal Amount { get; set; }
    public required string Category { get; set; }
    public string? Description { get; set; }
    public DateOnly ExpenseDate { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public Family Family { get; set; } = null!;
    public User CreatedByUser { get; set; } = null!;

    public static Expense Create(
        Guid familyId,
        Guid createdByUserId,
        decimal amount,
        string category,
        string? description,
        DateOnly expenseDate)
    {
        if (familyId == Guid.Empty)
        {
            throw new ArgumentException(
                "Family ID must not be empty.",
                nameof(familyId));
        }

        if (createdByUserId == Guid.Empty)
        {
            throw new ArgumentException(
                "Creator user ID must not be empty.",
                nameof(createdByUserId));
        }

        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                "Expense amount must be greater than zero.");
        }

        if (decimal.Round(amount, 2) != amount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                "Expense amount must have at most two decimal places.");
        }

        if (string.IsNullOrWhiteSpace(category))
        {
            throw new ArgumentException(
                "Category is required.",
                nameof(category));
        }

        if (category.Length > 50)
        {
            throw new ArgumentException(
                "Category must not exceed 50 characters.",
                nameof(category));
        }

        if (description?.Length > 500)
        {
            throw new ArgumentException(
                "Description must not exceed 500 characters.",
                nameof(description));
        }

        return new Expense
        {
            FamilyId = familyId,
            CreatedByUserId = createdByUserId,
            Amount = amount,
            Category = category,
            Description = description,
            ExpenseDate = expenseDate
        };
    }

    public void Update(
        decimal amount,
        string category,
        string? description,
        DateOnly expenseDate)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                "Expense amount must be greater than zero.");
        }

        if (decimal.Round(amount, 2) != amount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                "Expense amount must have at most two decimal places.");
        }

        if (string.IsNullOrWhiteSpace(category))
        {
            throw new ArgumentException(
                "Category is required.",
                nameof(category));
        }

        if (category.Length > 50)
        {
            throw new ArgumentException(
                "Category must not exceed 50 characters.",
                nameof(category));
        }

        if (description?.Length > 500)
        {
            throw new ArgumentException(
                "Description must not exceed 500 characters.",
                nameof(description));
        }

        Amount = amount;
        Category = category;
        Description = description;
        ExpenseDate = expenseDate;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }
}
