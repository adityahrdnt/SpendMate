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

    [Fact]
    public async Task UpdateAsync_WhenUserIsMember_UpdatesExpense()
    {
        _membershipRepository
            .Setup(x => x.IsMemberAsync(
                _familyId,
                _userId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var expense = Expense.Create(
            _familyId,
            _userId,
            35_000m,
            "Food",
            "Lunch",
            new DateOnly(2026, 10, 28));

        _expenseRepository
            .Setup(x => x.GetByIdAsync(
                _familyId,
                expense.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expense);

        _expenseRepository
            .Setup(x => x.UpdateAsync(
                It.IsAny<Expense>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Expense e, CancellationToken _) => e);

        var service = CreateService();

        var result = await service.UpdateAsync(
            _familyId,
            expense.Id,
            new UpdateExpenseRequest(
                50_000m,
                "Transport",
                "Taxi",
                new DateOnly(2026, 10, 29)));

        Assert.Equal(50_000m, result.Amount);
        Assert.Equal("Transport", result.Category);
        Assert.Equal("Taxi", result.Description);
        Assert.Equal(new DateOnly(2026, 10, 29), result.ExpenseDate);

        Assert.NotNull(expense.UpdatedAtUtc);

        _expenseRepository.Verify(
            x => x.UpdateAsync(
                It.Is<Expense>(e =>
                    e.Id == expense.Id &&
                    e.FamilyId == _familyId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WhenUserIsNotMember_DoesNotReadOrUpdateExpense()
    {
        _membershipRepository
            .Setup(x => x.IsMemberAsync(
                _familyId,
                _userId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var service = CreateService();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.UpdateAsync(
                _familyId,
                Guid.NewGuid(),
                new UpdateExpenseRequest(
                    50_000m,
                    "Transport",
                    null,
                    new DateOnly(2026, 10, 29))));

        _expenseRepository.Verify(
            x => x.GetByIdAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        _expenseRepository.Verify(
            x => x.UpdateAsync(
                It.IsAny<Expense>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_WhenExpenseDoesNotExist_ThrowsKeyNotFoundException()
    {
        _membershipRepository
            .Setup(x => x.IsMemberAsync(
                _familyId,
                _userId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var expenseId = Guid.NewGuid();

        _expenseRepository
            .Setup(x => x.GetByIdAsync(
                _familyId,
                expenseId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Expense?)null);

        var service = CreateService();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.UpdateAsync(
                _familyId,
                expenseId,
                new UpdateExpenseRequest(
                    50_000m,
                    "Transport",
                    null,
                    new DateOnly(2026, 10, 29))));

        _expenseRepository.Verify(
            x => x.UpdateAsync(
                It.IsAny<Expense>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_WhenUserIsMember_DeletesExpense()
    {
        _membershipRepository
            .Setup(x => x.IsMemberAsync(
                _familyId,
                _userId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var expense = Expense.Create(
            _familyId,
            _userId,
            35_000m,
            "Food",
            "Lunch",
            new DateOnly(2026, 10, 28));

        _expenseRepository
            .Setup(x => x.GetByIdAsync(
                _familyId,
                expense.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expense);

        _expenseRepository
            .Setup(x => x.DeleteAsync(
                expense,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = CreateService();

        await service.DeleteAsync(_familyId, expense.Id);

        _expenseRepository.Verify(
            x => x.DeleteAsync(
                expense,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_WhenUserIsNotMember_DoesNotReadOrDeleteExpense()
    {
        _membershipRepository
            .Setup(x => x.IsMemberAsync(
                _familyId,
                _userId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var service = CreateService();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.DeleteAsync(_familyId, Guid.NewGuid()));

        _expenseRepository.Verify(
            x => x.GetByIdAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        _expenseRepository.Verify(
            x => x.DeleteAsync(
                It.IsAny<Expense>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_WhenExpenseDoesNotExist_ThrowsKeyNotFoundException()
    {
        _membershipRepository
            .Setup(x => x.IsMemberAsync(
                _familyId,
                _userId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var expenseId = Guid.NewGuid();

        _expenseRepository
            .Setup(x => x.GetByIdAsync(
                _familyId,
                expenseId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Expense?)null);

        var service = CreateService();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.DeleteAsync(_familyId, expenseId));

        _expenseRepository.Verify(
            x => x.DeleteAsync(
                It.IsAny<Expense>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_WhenRepositoryReturnsFalse_ThrowsKeyNotFoundException()
    {
        _membershipRepository
            .Setup(x => x.IsMemberAsync(
                _familyId,
                _userId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var expense = Expense.Create(
            _familyId,
            _userId,
            35_000m,
            "Food",
            "Lunch",
            new DateOnly(2026, 10, 28));

        _expenseRepository
            .Setup(x => x.GetByIdAsync(
                _familyId,
                expense.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expense);

        _expenseRepository
            .Setup(x => x.DeleteAsync(
                expense,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var service = CreateService();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.DeleteAsync(_familyId, expense.Id));

        _expenseRepository.Verify(
            x => x.DeleteAsync(
                expense,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetByFamilyAsync_WhenFamilyIdIsEmpty_ThrowsArgumentException()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GetByFamilyAsync(Guid.Empty));

        _membershipRepository.Verify(
            x => x.IsMemberAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetByFamilyAsync_WhenCurrentUserIsUnauthenticated_ThrowsInvalidOperationException()
    {
        var service = CreateService();

        _currentUser.SetupGet(x => x.UserId).Returns(Guid.Empty);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GetByFamilyAsync(_familyId));

        _membershipRepository.Verify(
            x => x.IsMemberAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_WhenExpenseIsNotFoundInRequestedFamily_ThrowsKeyNotFoundException()
    {
        var expenseId = Guid.NewGuid();

        _membershipRepository
            .Setup(x => x.IsMemberAsync(
                _familyId,
                _userId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Repository tidak menemukan expense dalam family yang diminta.
        _expenseRepository
            .Setup(x => x.GetByIdAsync(
                _familyId,
                expenseId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Expense?)null);

        var service = CreateService();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.UpdateAsync(
                _familyId,
                expenseId,
                new UpdateExpenseRequest(
                    50_000m,
                    "Transport",
                    null,
                    new DateOnly(2026, 10, 29))));

        _expenseRepository.Verify(
            x => x.GetByIdAsync(
                _familyId,
                expenseId,
                It.IsAny<CancellationToken>()),
            Times.Once);

        _expenseRepository.Verify(
            x => x.UpdateAsync(
                It.IsAny<Expense>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
