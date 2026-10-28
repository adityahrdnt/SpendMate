using Moq;
using SpendMate.Application.Abstractions;
using SpendMate.Application.Expenses.Dtos;
using SpendMate.Application.Expenses.Services;
using SpendMate.Domain.Entities;

namespace SpendMate.UnitTests.Expenses;

public class ExpenseServiceTests
{
    private readonly Mock<IExpenseRepository> _expenseRepository = new();
    private readonly Mock<IFamilyMembershipRepository> _membershipRepository = new();
    private readonly Mock<ICurrentUser> _currentUser = new();

    private readonly Guid _familyId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    private ExpenseService CreateService()
    {
        _currentUser.SetupGet(x => x.UserId).Returns(_userId);

        return new ExpenseService(
            _expenseRepository.Object,
            _membershipRepository.Object,
            _currentUser.Object);
    }

    [Fact]
    public async Task CreateAsync_WhenUserIsMember_SavesExpense()
    {
        _membershipRepository
            .Setup(x => x.IsMemberAsync(
                _familyId,
                _userId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var savedExpense = Expense.Create(
            _familyId,
            _userId,
            35_000m,
            "Food",
            "Lunch",
            new DateOnly(2026, 10, 28));

        _expenseRepository
            .Setup(x => x.AddAsync(
                It.IsAny<Expense>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(savedExpense);

        var service = CreateService();

        var result = await service.CreateAsync(
            _familyId,
            new CreateExpenseRequest(
                35_000m,
                "Food",
                "Lunch",
                new DateOnly(2026, 10, 28)));

        Assert.Equal(35_000m, result.Amount);
        Assert.Equal(_familyId, result.FamilyId);
        Assert.Equal(_userId, result.CreatedByUserId);

        _expenseRepository.Verify(
            x => x.AddAsync(
                It.Is<Expense>(e =>
                    e.FamilyId == _familyId &&
                    e.CreatedByUserId == _userId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WhenUserIsNotMember_ThrowsAndDoesNotSave()
    {
        _membershipRepository
            .Setup(x => x.IsMemberAsync(
                _familyId,
                _userId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var service = CreateService();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.CreateAsync(
                _familyId,
                new CreateExpenseRequest(
                    35_000m,
                    "Food",
                    null,
                    new DateOnly(2026, 10, 28))));

        _expenseRepository.Verify(
            x => x.AddAsync(
                It.IsAny<Expense>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetByFamilyAsync_WhenUserIsNotMember_DoesNotReadExpenses()
    {
        _membershipRepository
            .Setup(x => x.IsMemberAsync(
                _familyId,
                _userId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var service = CreateService();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.GetByFamilyAsync(_familyId));

        _expenseRepository.Verify(
            x => x.GetByFamilyAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
