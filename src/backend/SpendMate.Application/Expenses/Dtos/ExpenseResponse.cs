using SpendMate.Domain.Entities;

namespace SpendMate.Application.Expenses.Dtos;

public sealed record ExpenseResponse(
    Guid Id,
    Guid FamilyId,
    Guid CreatedByUserId,
    decimal Amount,
    string Category,
    string? Description,
    DateOnly ExpenseDate,
    DateTimeOffset CreatedAtUtc)
{
    public static ExpenseResponse FromEntity(Expense expense) =>
        new(
            expense.Id,
            expense.FamilyId,
            expense.CreatedByUserId,
            expense.Amount,
            expense.Category,
            expense.Description,
            expense.ExpenseDate,
            expense.CreatedAtUtc);
}
