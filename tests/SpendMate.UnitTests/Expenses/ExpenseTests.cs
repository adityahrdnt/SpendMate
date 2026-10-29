using SpendMate.Domain.Entities;

namespace SpendMate.UnitTests.Expenses;

public class ExpenseTests
{
    [Fact]
    public void Create_WithValidData_CreatesExpense()
    {
        var expenseDate = new DateOnly(2026, 10, 28);

        var expense = Expense.Create(
            familyId: Guid.NewGuid(),
            createdByUserId: Guid.NewGuid(),
            amount: 35_000m,
            category: "Food",
            description: "Lunch",
            expenseDate: expenseDate);

        Assert.NotEqual(Guid.Empty, expense.Id);
        Assert.Equal(35_000m, expense.Amount);
        Assert.Equal("Food", expense.Category);
        Assert.Equal("Lunch", expense.Description);
        Assert.Equal(expenseDate, expense.ExpenseDate);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Create_WithNonPositiveAmount_ThrowsArgumentOutOfRangeException(
        decimal amount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Expense.Create(
                Guid.NewGuid(),
                Guid.NewGuid(),
                amount,
                "Food",
                null,
                new DateOnly(2026, 10, 28)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankCategory_ThrowsArgumentException(
        string category)
    {
        Assert.Throws<ArgumentException>(() =>
            Expense.Create(
                Guid.NewGuid(),
                Guid.NewGuid(),
                35_000m,
                category,
                null,
                new DateOnly(2026, 10, 28)));
    }

    [Fact]
    public void Create_WithCategoryExceeding50Characters_ThrowsArgumentException()
    {
        var category = new string('A', 51);

        Assert.Throws<ArgumentException>(() =>
            Expense.Create(
                Guid.NewGuid(),
                Guid.NewGuid(),
                35_000m,
                category,
                null,
                new DateOnly(2026, 10, 28)));
    }

    [Fact]
    public void Create_WithDescriptionExceeding500Characters_ThrowsArgumentException()
    {
        var description = new string('A', 501);

        Assert.Throws<ArgumentException>(() =>
            Expense.Create(
                Guid.NewGuid(),
                Guid.NewGuid(),
                35_000m,
                "Food",
                description,
                new DateOnly(2026, 10, 28)));
    }

    [Fact]
    public void Create_WithNullDescription_AllowsEmptyDescription()
    {
        var expense = Expense.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            35_000m,
            "Food",
            null,
            new DateOnly(2026, 10, 28));

        Assert.Null(expense.Description);
    }

    [Theory]
    [InlineData(0.001)]
    [InlineData(10.123)]
    public void Create_WithMoreThanTwoDecimalPlaces_ThrowsArgumentOutOfRangeException(
        decimal amount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Expense.Create(
                Guid.NewGuid(),
                Guid.NewGuid(),
                amount,
                "Food",
                null,
                new DateOnly(2026, 10, 28)));
    }

    [Fact]
    public void Create_WithEmptyFamilyId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            Expense.Create(
                Guid.Empty,
                Guid.NewGuid(),
                35_000m,
                "Food",
                null,
                new DateOnly(2026, 10, 28)));
    }

    [Fact]
    public void Create_WithEmptyCreatorId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            Expense.Create(
                Guid.NewGuid(),
                Guid.Empty,
                35_000m,
                "Food",
                null,
                new DateOnly(2026, 10, 28)));
    }

    [Fact]
    public void Create_WithCategoryAtMaximumLength_CreatesExpense()
    {
        var category = new string('A', 50);

        var expense = Expense.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            35_000m,
            category,
            null,
            new DateOnly(2026, 10, 28));

        Assert.Equal(category, expense.Category);
    }

    [Fact]
    public void Create_WithDescriptionAtMaximumLength_CreatesExpense()
    {
        var description = new string('A', 500);

        var expense = Expense.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            35_000m,
            "Food",
            description,
            new DateOnly(2026, 10, 28));

        Assert.Equal(description, expense.Description);
    }
}
