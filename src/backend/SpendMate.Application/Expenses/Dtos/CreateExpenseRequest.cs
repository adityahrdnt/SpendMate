namespace SpendMate.Application.Expenses.Dtos;

public sealed record CreateExpenseRequest(
    decimal Amount,
    string Category,
    string? Description,
    DateOnly ExpenseDate);
