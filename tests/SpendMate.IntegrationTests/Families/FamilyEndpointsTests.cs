using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Json;
using SpendMate.Application.Families.Dtos;
using SpendMate.IntegrationTests.Support;
using Xunit;

namespace SpendMate.IntegrationTests.Families;

public sealed class FamilyEndpointsTests(
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
    public async Task GetFamilies_AsAuthenticatedUser_ReturnsOwnFamilies()
    {
        // Act
        var response = await _client.GetAsync("/api/families");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var families =
            await response.Content.ReadFromJsonAsync<List<FamilyResponse>>();

        Assert.NotNull(families);

        var personalFamily = Assert.Single(
            families,
            item => item.Id == SpendMateApiFactory.TestFamilyId);

        Assert.Equal("Integration Test Family", personalFamily.Name);
        Assert.Equal("Personal", personalFamily.Type);
        Assert.Equal("Owner", personalFamily.Role);

        // Families belonging to another user must not be returned.
        Assert.DoesNotContain(
            families,
            item => item.Id == SpendMateApiFactory.NonMemberFamilyId);

        Assert.DoesNotContain(
            families,
            item => item.Id == SpendMateApiFactory.CrossFamilyId);
    }

    [Fact]
    public async Task GetFamilies_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/families");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateFamily_WithValidRequest_ReturnsCreatedAndPersistsFamily()
    {
        // Arrange
        var request = new CreateFamilyRequest("New Shared Family");

        // Act
        var response = await _client.PostAsJsonAsync(
            "/api/families",
            request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var createdFamily =
            await response.Content.ReadFromJsonAsync<FamilyResponse>();

        Assert.NotNull(createdFamily);
        Assert.NotEqual(Guid.Empty, createdFamily.Id);
        Assert.Equal("New Shared Family", createdFamily.Name);
        Assert.Equal("Shared", createdFamily.Type);
        Assert.Equal("Owner", createdFamily.Role);
        Assert.NotEqual(default, createdFamily.JoinedAtUtc);

        // Verify that the created family is persisted and visible to its owner.
        var getResponse = await _client.GetAsync("/api/families");

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var families =
            await getResponse.Content.ReadFromJsonAsync<List<FamilyResponse>>();

        Assert.NotNull(families);

        var persistedFamily = Assert.Single(
            families,
            family => family.Id == createdFamily.Id);

        Assert.Equal(createdFamily.Name, persistedFamily.Name);
        Assert.Equal("Shared", persistedFamily.Type);
        Assert.Equal("Owner", persistedFamily.Role);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public async Task CreateFamily_WithEmptyName_ReturnsBadRequest(string name)
    {
        // Arrange
        var request = new CreateFamilyRequest(name);

        // Act
        var response = await _client.PostAsJsonAsync(
            "/api/families",
            request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateFamily_WithNameExceedingMaximumLength_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateFamilyRequest(new string('A', 101));

        // Act
        var response = await _client.PostAsJsonAsync(
            "/api/families",
            request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateFamily_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            });

        // Obtain a valid CSRF token without authenticating.
        var csrfResponse = await client.GetAsync("/api/auth/csrf");

        Assert.Equal(HttpStatusCode.OK, csrfResponse.StatusCode);

        var csrfToken = await csrfResponse.Content
            .ReadFromJsonAsync<CsrfTokenResponse>();

        Assert.NotNull(csrfToken);
        Assert.False(string.IsNullOrWhiteSpace(csrfToken.RequestToken));

        client.DefaultRequestHeaders.Add(
            "X-CSRF-TOKEN",
            csrfToken.RequestToken);

        var request = new CreateFamilyRequest("Unauthorized Family");

        // Act
        var response = await client.PostAsJsonAsync(
            "/api/families",
            request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateFamily_WithoutCsrfToken_ReturnsBadRequest()
    {
        // Arrange
        using var client = factory.CreateAuthenticatedClient();

        var request = new CreateFamilyRequest("Family Without CSRF");

        // Act
        var response = await client.PostAsJsonAsync(
            "/api/families",
            request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private sealed record CsrfTokenResponse(string RequestToken);
}
