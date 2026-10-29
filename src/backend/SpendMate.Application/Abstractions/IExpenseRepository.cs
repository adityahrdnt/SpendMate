using SpendMate.Domain.Entities;

namespace SpendMate.Application.Abstractions;

public interface IExpenseRepository
{
    Task<IReadOnlyList<Expense>> GetByFamilyAsync(
        Guid familyId,
        CancellationToken cancellationToken = default);

    Task<Expense?> GetByIdAsync(
        Guid familyId,
        Guid expenseId,
        CancellationToken cancellationToken = default);

    Task<Expense> AddAsync(
        Expense expense,
        CancellationToken cancellationToken = default);

    Task<Expense> UpdateAsync(
        Expense expense,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        Expense expense,
        CancellationToken cancellationToken = default);
}
