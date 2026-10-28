using SpendMate.Domain.Entities;

namespace SpendMate.Application.Abstractions;

public interface IExpenseRepository
{
    Task<IReadOnlyList<Expense>> GetByFamilyAsync(
        Guid familyId,
        CancellationToken cancellationToken = default);

    Task<Expense> AddAsync(
        Expense expense,
        CancellationToken cancellationToken = default);
}
