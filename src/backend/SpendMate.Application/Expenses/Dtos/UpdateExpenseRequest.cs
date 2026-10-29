namespace SpendMate.Application.Expenses.Dtos;

public sealed record UpdateExpenseRequest(
    decimal Amount,
    string Category,
    string? Description,
    DateOnly ExpenseDate);
