using SpendMate.Application.Abstractions;
using SpendMate.Application.Expenses.Dtos;
using SpendMate.Domain.Entities;

namespace SpendMate.Application.Expenses.Services;

public sealed class ExpenseService(
    IExpenseRepository expenseRepository,
    IFamilyMembershipRepository membershipRepository,
    ICurrentUser currentUser)
{
    public async Task<IReadOnlyList<ExpenseResponse>> GetByFamilyAsync(
        Guid familyId,
        CancellationToken cancellationToken = default)
    {
        await EnsureMembershipAsync(familyId, cancellationToken);

        var expenses = await expenseRepository.GetByFamilyAsync(
            familyId,
            cancellationToken);

        return expenses
            .Select(ExpenseResponse.FromEntity)
            .ToList();
    }

    public async Task<ExpenseResponse> CreateAsync(
        Guid familyId,
        CreateExpenseRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureMembershipAsync(familyId, cancellationToken);

        var expense = Expense.Create(
            familyId,
            currentUser.UserId,
            request.Amount,
            request.Category,
            request.Description,
            request.ExpenseDate);

        var savedExpense = await expenseRepository.AddAsync(
            expense,
            cancellationToken);

        return ExpenseResponse.FromEntity(savedExpense);
    }

    private async Task EnsureMembershipAsync(
        Guid familyId,
        CancellationToken cancellationToken)
    {
        if (familyId == Guid.Empty)
        {
            throw new ArgumentException(
                "Family ID must not be empty.",
                nameof(familyId));
        }

        if (currentUser.UserId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "The current user is not authenticated.");
        }

        var isMember = await membershipRepository.IsMemberAsync(
            familyId,
            currentUser.UserId,
            cancellationToken);

        if (!isMember)
        {
            throw new UnauthorizedAccessException(
                "The current user is not a member of this family.");
        }
    }
}
