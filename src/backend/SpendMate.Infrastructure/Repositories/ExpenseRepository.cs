using Microsoft.EntityFrameworkCore;
using SpendMate.Application.Abstractions;
using SpendMate.Domain.Entities;
using SpendMate.Infrastructure.Data;

namespace SpendMate.Infrastructure.Repositories;

public sealed class ExpenseRepository(
    SpendMateDbContext dbContext) : IExpenseRepository
{
    public async Task<IReadOnlyList<Expense>> GetByFamilyAsync(
        Guid familyId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Expenses
            .AsNoTracking()
            .Where(x => x.FamilyId == familyId)
            .OrderByDescending(x => x.ExpenseDate)
            .ThenByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<Expense> AddAsync(
        Expense expense,
        CancellationToken cancellationToken = default)
    {
        dbContext.Expenses.Add(expense);

        await dbContext.SaveChangesAsync(cancellationToken);

        return expense;
    }
}
