using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using SpendMate.Application.Expenses.Dtos;
using SpendMate.IntegrationTests.Support;
using Xunit;

namespace SpendMate.IntegrationTests.Expenses;

public sealed class ExpenseEndpointsTests(
    SpendMateApiFactory factory)
    : IClassFixture<SpendMateApiFactory>, IAsyncLifetime
{
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _client = await factory.CreateAuthenticatedClientWithCsrfAsync();
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

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

    [Fact]
    public async Task UpdateExpense_AsFamilyMember_ReturnsUpdatedExpense()
    {
        // Arrange
        var familyId = SpendMateApiFactory.TestFamilyId;

        var createRequest = new CreateExpenseRequest(
            Amount: 35000m,
            Category: "Food",
            Description: "Original description",
            ExpenseDate: new DateOnly(2026, 10, 28));

        var createResponse = await _client.PostAsJsonAsync(
            $"/api/families/{familyId}/expenses",
            createRequest);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var createdExpense =
            await createResponse.Content.ReadFromJsonAsync<ExpenseResponse>();

        Assert.NotNull(createdExpense);

        var updateRequest = new UpdateExpenseRequest(
            Amount: 50000m,
            Category: "Transport",
            Description: "Updated description",
            ExpenseDate: new DateOnly(2026, 10, 29));

        // Act
        var updateResponse = await _client.PutAsJsonAsync(
            $"/api/families/{familyId}/expenses/{createdExpense.Id}",
            updateRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var updatedExpense =
            await updateResponse.Content.ReadFromJsonAsync<ExpenseResponse>();

        Assert.NotNull(updatedExpense);
        Assert.Equal(createdExpense.Id, updatedExpense.Id);
        Assert.Equal(familyId, updatedExpense.FamilyId);
        Assert.Equal(createdExpense.CreatedByUserId, updatedExpense.CreatedByUserId);
        Assert.Equal(50000m, updatedExpense.Amount);
        Assert.Equal("Transport", updatedExpense.Category);
        Assert.Equal("Updated description", updatedExpense.Description);
        Assert.Equal(new DateOnly(2026, 10, 29), updatedExpense.ExpenseDate);

        // Verify the updated values are persisted.
        var getResponse = await _client.GetAsync(
            $"/api/families/{familyId}/expenses");

        var expenses =
            await getResponse.Content.ReadFromJsonAsync<List<ExpenseResponse>>();

        Assert.NotNull(expenses);

        var persistedExpense = Assert.Single(
            expenses,
            expense => expense.Id == createdExpense.Id);

        Assert.Equal(50000m, persistedExpense.Amount);
        Assert.Equal("Transport", persistedExpense.Category);
    }

    [Fact]
    public async Task UpdateExpense_WithZeroAmount_ReturnsBadRequest()
    {
        // Arrange
        var familyId = SpendMateApiFactory.TestFamilyId;

        var createResponse = await _client.PostAsJsonAsync(
            $"/api/families/{familyId}/expenses",
            new CreateExpenseRequest(
                Amount: 25000m,
                Category: "Food",
                Description: "Valid expense",
                ExpenseDate: new DateOnly(2026, 10, 28)));

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var createdExpense =
            await createResponse.Content.ReadFromJsonAsync<ExpenseResponse>();

        Assert.NotNull(createdExpense);

        var updateRequest = new UpdateExpenseRequest(
            Amount: 0m,
            Category: "Transport",
            Description: "Invalid update",
            ExpenseDate: new DateOnly(2026, 10, 29));

        // Act
        var response = await _client.PutAsJsonAsync(
            $"/api/families/{familyId}/expenses/{createdExpense.Id}",
            updateRequest);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateExpense_AsNonMember_ReturnsForbidden()
    {
        // Arrange
        var validFamilyId = SpendMateApiFactory.TestFamilyId;

        var createResponse = await _client.PostAsJsonAsync(
            $"/api/families/{validFamilyId}/expenses",
            new CreateExpenseRequest(
                Amount: 25000m,
                Category: "Food",
                Description: "Member expense",
                ExpenseDate: new DateOnly(2026, 10, 28)));

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var createdExpense =
            await createResponse.Content.ReadFromJsonAsync<ExpenseResponse>();

        Assert.NotNull(createdExpense);

        // Act: use a family the current user does not belong to.
        var response = await _client.PutAsJsonAsync(
            $"/api/families/{SpendMateApiFactory.NonMemberFamilyId}/expenses/{createdExpense.Id}",
            new UpdateExpenseRequest(
                Amount: 40000m,
                Category: "Transport",
                Description: "Unauthorized update",
                ExpenseDate: new DateOnly(2026, 10, 29)));

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        // Verify the original expense remains unchanged.
        var getResponse = await _client.GetAsync(
            $"/api/families/{validFamilyId}/expenses");

        var expenses =
            await getResponse.Content.ReadFromJsonAsync<List<ExpenseResponse>>();

        Assert.NotNull(expenses);

        var persistedExpense = Assert.Single(
            expenses,
            expense => expense.Id == createdExpense.Id);

        Assert.Equal(25000m, persistedExpense.Amount);
        Assert.Equal("Food", persistedExpense.Category);
    }

    [Fact]
    public async Task UpdateExpense_WhenExpenseDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        var familyId = SpendMateApiFactory.TestFamilyId;
        var nonexistentExpenseId = Guid.NewGuid();

        var request = new UpdateExpenseRequest(
            Amount: 40000m,
            Category: "Transport",
            Description: "Does not exist",
            ExpenseDate: new DateOnly(2026, 10, 29));

        // Act
        var response = await _client.PutAsJsonAsync(
            $"/api/families/{familyId}/expenses/{nonexistentExpenseId}",
            request);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateExpense_WhenExpenseBelongsToAnotherFamily_ReturnsNotFound()
    {
        // Arrange
        var expenseFamilyId = SpendMateApiFactory.CrossFamilyId;
        var requestedFamilyId = SpendMateApiFactory.TestFamilyId;

        using var ownerClient =
            await factory.CreateAuthenticatedClientWithCsrfAsync(
                SpendMateApiFactory.SecondTestUserId);

        var createResponse = await ownerClient.PostAsJsonAsync(
            $"/api/families/{expenseFamilyId}/expenses",
            new CreateExpenseRequest(
                Amount: 25000m,
                Category: "Food",
                Description: "Cross-family test expense",
                ExpenseDate: new DateOnly(2026, 10, 28)));

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var createdExpense =
            await createResponse.Content.ReadFromJsonAsync<ExpenseResponse>();

        Assert.NotNull(createdExpense);

        // Act: access the expense through a different family route.
        var response = await _client.PutAsJsonAsync(
            $"/api/families/{requestedFamilyId}/expenses/{createdExpense.Id}",
            new UpdateExpenseRequest(
                Amount: 40000m,
                Category: "Transport",
                Description: "Should not be updated",
                ExpenseDate: new DateOnly(2026, 10, 29)));

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        // Verify the original expense remains unchanged.
        var getResponse = await ownerClient.GetAsync(
            $"/api/families/{expenseFamilyId}/expenses");

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var expenses =
            await getResponse.Content.ReadFromJsonAsync<List<ExpenseResponse>>();

        Assert.NotNull(expenses);

        var persistedExpense = Assert.Single(
            expenses,
            expense => expense.Id == createdExpense.Id);

        Assert.Equal(25000m, persistedExpense.Amount);
        Assert.Equal("Food", persistedExpense.Category);
        Assert.Equal("Cross-family test expense", persistedExpense.Description);
    }

    [Fact]
    public async Task DeleteExpense_WhenExpenseBelongsToAnotherFamily_ReturnsNotFound()
    {
        // Arrange
        var expenseFamilyId = SpendMateApiFactory.CrossFamilyId;
        var requestedFamilyId = SpendMateApiFactory.TestFamilyId;

        using var ownerClient =
            await factory.CreateAuthenticatedClientWithCsrfAsync(
                SpendMateApiFactory.SecondTestUserId);

        var createResponse = await ownerClient.PostAsJsonAsync(
            $"/api/families/{expenseFamilyId}/expenses",
            new CreateExpenseRequest(
                Amount: 25000m,
                Category: "Food",
                Description: "Cross-family test expense",
                ExpenseDate: new DateOnly(2026, 10, 28)));

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var createdExpense =
            await createResponse.Content.ReadFromJsonAsync<ExpenseResponse>();

        Assert.NotNull(createdExpense);

        // Act: attempt deletion through another family route.
        var response = await _client.DeleteAsync(
            $"/api/families/{requestedFamilyId}/expenses/{createdExpense.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        // Verify the original expense still exists.
        var getResponse = await ownerClient.GetAsync(
            $"/api/families/{expenseFamilyId}/expenses");

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var expenses =
            await getResponse.Content.ReadFromJsonAsync<List<ExpenseResponse>>();

        Assert.NotNull(expenses);

        var persistedExpense = Assert.Single(
            expenses,
            expense => expense.Id == createdExpense.Id);

        Assert.Equal(25000m, persistedExpense.Amount);
        Assert.Equal("Food", persistedExpense.Category);
        Assert.Equal("Cross-family test expense", persistedExpense.Description);
    }

    [Fact]
    public async Task DeleteExpense_AsFamilyMember_ReturnsNoContentAndRemovesExpense()
    {
        // Arrange
        var familyId = SpendMateApiFactory.TestFamilyId;

        var createResponse = await _client.PostAsJsonAsync(
            $"/api/families/{familyId}/expenses",
            new CreateExpenseRequest(
                Amount: 25000m,
                Category: "Food",
                Description: "Expense to delete",
                ExpenseDate: new DateOnly(2026, 10, 28)));

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var createdExpense =
            await createResponse.Content.ReadFromJsonAsync<ExpenseResponse>();

        Assert.NotNull(createdExpense);

        // Act
        var deleteResponse = await _client.DeleteAsync(
            $"/api/families/{familyId}/expenses/{createdExpense.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var getResponse = await _client.GetAsync(
            $"/api/families/{familyId}/expenses");

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var expenses =
            await getResponse.Content.ReadFromJsonAsync<List<ExpenseResponse>>();

        Assert.NotNull(expenses);
        Assert.DoesNotContain(
            expenses,
            expense => expense.Id == createdExpense.Id);
    }

    [Fact]
    public async Task DeleteExpense_AsNonMember_ReturnsForbidden()
    {
        // Arrange
        var validFamilyId = SpendMateApiFactory.TestFamilyId;

        var createResponse = await _client.PostAsJsonAsync(
            $"/api/families/{validFamilyId}/expenses",
            new CreateExpenseRequest(
                Amount: 25000m,
                Category: "Food",
                Description: "Member expense",
                ExpenseDate: new DateOnly(2026, 10, 28)));

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var createdExpense =
            await createResponse.Content.ReadFromJsonAsync<ExpenseResponse>();

        Assert.NotNull(createdExpense);

        // Act
        var response = await _client.DeleteAsync(
            $"/api/families/{SpendMateApiFactory.NonMemberFamilyId}/expenses/{createdExpense.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        // Verify the expense still exists in its original family.
        var getResponse = await _client.GetAsync(
            $"/api/families/{validFamilyId}/expenses");

        var expenses =
            await getResponse.Content.ReadFromJsonAsync<List<ExpenseResponse>>();

        Assert.NotNull(expenses);
        Assert.Contains(
            expenses,
            expense => expense.Id == createdExpense.Id);
    }

    [Fact]
    public async Task DeleteExpense_WhenExpenseDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        var familyId = SpendMateApiFactory.TestFamilyId;
        var nonexistentExpenseId = Guid.NewGuid();

        // Act
        var response = await _client.DeleteAsync(
            $"/api/families/{familyId}/expenses/{nonexistentExpenseId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetExpenses_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        using var unauthenticatedClient = factory.CreateClient();

        // Act
        var response = await unauthenticatedClient.GetAsync(
            $"/api/families/{SpendMateApiFactory.TestFamilyId}/expenses");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateExpense_WithoutCsrfToken_ReturnsBadRequest()
    {
        // Arrange
        using var client = factory.CreateAuthenticatedClient();

        var request = new CreateExpenseRequest(
            Amount: 35000m,
            Category: "Food",
            Description: "Request without CSRF token",
            ExpenseDate: new DateOnly(2026, 10, 28));

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/families/{SpendMateApiFactory.TestFamilyId}/expenses",
            request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetCsrfToken_ReturnsRequestToken()
    {
        // Arrange
        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            });

        // Act
        var response = await client.GetAsync("/api/auth/csrf");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content
            .ReadFromJsonAsync<CsrfTokenResponse>();

        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result.RequestToken));
    }

    [Fact]
    public async Task GetExpenses_AsMemberOfAnotherFamily_ReturnsForbidden()
    {
        // Arrange
        var familyId = SpendMateApiFactory.CrossFamilyId;

        // Act
        var response = await _client.GetAsync(
            $"/api/families/{familyId}/expenses");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateExpense_AsMemberOfAnotherFamily_ReturnsForbidden()
    {
        // Arrange
        var familyId = SpendMateApiFactory.CrossFamilyId;

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

    [Fact]
    public async Task UpdateExpense_AsMemberOfAnotherFamily_ReturnsForbidden()
    {
        // Arrange
        var familyId = SpendMateApiFactory.CrossFamilyId;

        using var ownerClient =
            await factory.CreateAuthenticatedClientWithCsrfAsync(
                SpendMateApiFactory.SecondTestUserId);

        var createResponse = await ownerClient.PostAsJsonAsync(
            $"/api/families/{familyId}/expenses",
            new CreateExpenseRequest(
                Amount: 25000m,
                Category: "Food",
                Description: "Authorization update test",
                ExpenseDate: new DateOnly(2026, 10, 28)));

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var createdExpense =
            await createResponse.Content.ReadFromJsonAsync<ExpenseResponse>();

        Assert.NotNull(createdExpense);

        // Act
        var response = await _client.PutAsJsonAsync(
            $"/api/families/{familyId}/expenses/{createdExpense.Id}",
            new UpdateExpenseRequest(
                Amount: 40000m,
                Category: "Transport",
                Description: "Should not be updated",
                ExpenseDate: new DateOnly(2026, 10, 29)));

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        // Verify the original expense remains unchanged.
        var getResponse = await ownerClient.GetAsync(
            $"/api/families/{familyId}/expenses");

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var expenses =
            await getResponse.Content.ReadFromJsonAsync<List<ExpenseResponse>>();

        Assert.NotNull(expenses);

        var persistedExpense = Assert.Single(
            expenses,
            expense => expense.Id == createdExpense.Id);

        Assert.Equal(25000m, persistedExpense.Amount);
        Assert.Equal("Food", persistedExpense.Category);
        Assert.Equal("Authorization update test", persistedExpense.Description);
    }

    [Fact]
    public async Task DeleteExpense_AsMemberOfAnotherFamily_ReturnsForbidden()
    {
        // Arrange
        var familyId = SpendMateApiFactory.CrossFamilyId;

        using var ownerClient =
            await factory.CreateAuthenticatedClientWithCsrfAsync(
                SpendMateApiFactory.SecondTestUserId);

        var createResponse = await ownerClient.PostAsJsonAsync(
            $"/api/families/{familyId}/expenses",
            new CreateExpenseRequest(
                Amount: 25000m,
                Category: "Food",
                Description: "Authorization delete test",
                ExpenseDate: new DateOnly(2026, 10, 28)));

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var createdExpense =
            await createResponse.Content.ReadFromJsonAsync<ExpenseResponse>();

        Assert.NotNull(createdExpense);

        // Act
        var response = await _client.DeleteAsync(
            $"/api/families/{familyId}/expenses/{createdExpense.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        // Verify the original expense still exists.
        var getResponse = await ownerClient.GetAsync(
            $"/api/families/{familyId}/expenses");

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var expenses =
            await getResponse.Content.ReadFromJsonAsync<List<ExpenseResponse>>();

        Assert.NotNull(expenses);

        var persistedExpense = Assert.Single(
            expenses,
            expense => expense.Id == createdExpense.Id);

        Assert.Equal(25000m, persistedExpense.Amount);
        Assert.Equal("Food", persistedExpense.Category);
        Assert.Equal("Authorization delete test", persistedExpense.Description);
    }

    private sealed record CsrfTokenResponse(string RequestToken);
}
