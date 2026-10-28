using System.Net;
using System.Net.Http.Json;
using SpendMate.Application.Expenses.Dtos;
using SpendMate.IntegrationTests.Support;
using Xunit;

namespace SpendMate.IntegrationTests;

public sealed class ExpenseEndpointsTests(
    SpendMateApiFactory factory) : IClassFixture<SpendMateApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task CreateExpense_AsFamilyMember_ReturnsCreatedAndPersistsExpense()
    {
        // Arrange
        var familyId = SpendMateApiFactory.TestFamilyId;

        var request = new CreateExpenseRequest(
            Amount: 35000m,
            Category: "Food",
            Description: "Integration test lunch",
            ExpenseDate: new DateOnly(2026, 10, 28));

        // Act
        var postResponse = await _client.PostAsJsonAsync(
            $"/api/families/{familyId}/expenses",
            request);

        // Assert: creation succeeds
        Assert.Equal(HttpStatusCode.Created, postResponse.StatusCode);

        var createdExpense =
            await postResponse.Content.ReadFromJsonAsync<ExpenseResponse>();

        Assert.NotNull(createdExpense);
        Assert.Equal(familyId, createdExpense.FamilyId);
        Assert.Equal(SpendMateApiFactory.TestUserId, createdExpense.CreatedByUserId);
        Assert.Equal(35000m, createdExpense.Amount);
        Assert.Equal("Food", createdExpense.Category);

        // Assert: the saved expense can be retrieved
        var getResponse = await _client.GetAsync(
            $"/api/families/{familyId}/expenses");

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var expenses =
            await getResponse.Content.ReadFromJsonAsync<List<ExpenseResponse>>();

        Assert.NotNull(expenses);
        Assert.Contains(expenses, expense => expense.Id == createdExpense.Id);
    }

    [Fact]
    public async Task CreateExpense_WithZeroAmount_ReturnsBadRequest()
    {
        // Arrange
        var familyId = SpendMateApiFactory.TestFamilyId;

        var request = new CreateExpenseRequest(
            Amount: 0m,
            Category: "Food",
            Description: "Invalid expense",
            ExpenseDate: new DateOnly(2026, 10, 28));

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/families/{familyId}/expenses",
            request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetExpenses_AsNonMember_ReturnsForbidden()
    {
        // Arrange
        var familyId = SpendMateApiFactory.NonMemberFamilyId;

        // Act
        var response = await _client.GetAsync(
            $"/api/families/{familyId}/expenses");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateExpense_AsNonMember_ReturnsForbidden()
    {
        // Arrange
        var familyId = SpendMateApiFactory.NonMemberFamilyId;

        var request = new CreateExpenseRequest(
            Amount: 25000m,
            Category: "Transport",
            Description: "Should be rejected",
            ExpenseDate: new DateOnly(2026, 10, 28));

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/families/{familyId}/expenses",
            request);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
