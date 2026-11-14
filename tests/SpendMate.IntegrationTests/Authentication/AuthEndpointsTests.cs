using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpendMate.Application.Abstractions;
using SpendMate.Application.Auth.Dtos;
using SpendMate.Application.Auth.Services;
using SpendMate.Application.Families.Dtos;
using SpendMate.Infrastructure.Data;
using SpendMate.IntegrationTests.Support;
using SpendMate.IntegrationTests.Support.Authentication;
using Xunit;

namespace SpendMate.IntegrationTests.Authentication;

public sealed class AuthEndpointsTests(
    SpendMateApiFactory factory)
    : IClassFixture<SpendMateApiFactory>
{
    [Fact]
    public async Task Register_WithValidRequest_ReturnsUserAndAuthenticationCookie()
    {
        // Arrange
        using var client = await CreateClientWithCsrfAsync();

        var request = new RegisterRequest(
            Email: $"register-{Guid.NewGuid():N}@spendmate.local",
            DisplayName: "New Test User",
            Password: "SecurePassword123!",
            FamilyName: "New Test Family");

        // Act
        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result =
            await response.Content.ReadFromJsonAsync<AuthResponse>();

        Assert.NotNull(result);
        Assert.Equal(request.Email, result.Email);
        Assert.Equal(request.DisplayName, result.DisplayName);
        Assert.Equal(request.FamilyName, result.FamilyName);
        Assert.NotEqual(Guid.Empty, result.UserId);
        Assert.NotEqual(Guid.Empty, result.FamilyId);

        Assert.Contains(
            response.Headers.GetValues("Set-Cookie"),
            cookie => cookie.Contains(
                ".SpendMate.Auth=",
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ReturnsUnauthorized()
    {
        // Arrange
        var email = $"invalid-login-{Guid.NewGuid():N}@spendmate.local";
        const string password = "SecurePassword123!";

        using var registrationClient = await CreateClientWithCsrfAsync();

        var registerResponse = await registrationClient.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(
                email,
                "Invalid Login User",
                password,
                "Invalid Login Family"));

        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

        using var loginClient = await CreateClientWithCsrfAsync();

        // Act
        var response = await loginClient.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, "WrongPassword123!"));

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ReturnsConflict()
    {
        // Arrange
        var email = $"duplicate-{Guid.NewGuid():N}@spendmate.local";

        using var client = await CreateClientWithCsrfAsync();

        var request = new RegisterRequest(
            email,
            "Duplicate Test User",
            "SecurePassword123!",
            "Duplicate Test Family");

        var firstResponse = await client.PostAsJsonAsync(
            "/api/auth/register",
            request);

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        await RefreshCsrfTokenAsync(client);

        // Act
        var secondResponse = await client.PostAsJsonAsync(
            "/api/auth/register",
            request);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
    }

    [Fact]
    public async Task Register_WithEmptyFamilyName_ReturnsBadRequest()
    {
        // Arrange
        using var client = await CreateClientWithCsrfAsync();

        var request = new RegisterRequest(
            $"invalid-register-{Guid.NewGuid():N}@spendmate.local",
            "Invalid Registration User",
            "SecurePassword123!",
            " ");

        // Act
        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Logout_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        using var client = await CreateClientWithCsrfAsync();

        // Act
        var response = await client.PostAsync(
            "/api/auth/logout",
            content: null);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<HttpClient> CreateClientWithCsrfAsync()
    {
        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            });

        var csrfResponse = await client.GetAsync("/api/auth/csrf");
        csrfResponse.EnsureSuccessStatusCode();

        var csrfResult = await csrfResponse.Content
            .ReadFromJsonAsync<CsrfTokenResponse>();

        if (string.IsNullOrWhiteSpace(csrfResult?.RequestToken))
        {
            client.Dispose();

            throw new InvalidOperationException(
                "Failed to obtain CSRF token.");
        }

        client.DefaultRequestHeaders.Add(
            "X-CSRF-TOKEN",
            csrfResult.RequestToken);

        return client;
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsUserAndAuthenticationCookie()
    {
        // Arrange
        var email = $"login-{Guid.NewGuid():N}@spendmate.local";
        const string password = "SecurePassword123!";

        using var registrationClient = await CreateClientWithCsrfAsync();

        var registerResponse = await registrationClient.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(
                Email: email,
                DisplayName: "Login Test User",
                Password: password,
                FamilyName: "Login Test Family"));

        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

        // Use a fresh client to simulate a new login session.
        using var loginClient = await CreateClientWithCsrfAsync();

        // Act
        var loginResponse = await loginClient.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, password));

        // Assert
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var result =
            await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();

        Assert.NotNull(result);
        Assert.Equal(email, result.Email);
        Assert.Equal("Login Test User", result.DisplayName);
        Assert.Equal("Login Test Family", result.FamilyName);

        Assert.Contains(
            loginResponse.Headers.GetValues("Set-Cookie"),
            cookie => cookie.Contains(
                ".SpendMate.Auth=",
                StringComparison.Ordinal));

        // Verify the cookie authenticates subsequent requests.
        var meResponse = await loginClient.GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);

        var currentUser =
            await meResponse.Content.ReadFromJsonAsync<AuthResponse>();

        Assert.NotNull(currentUser);
        Assert.Equal(result.UserId, currentUser.UserId);
        Assert.Equal(result.Email, currentUser.Email);
        Assert.Equal(result.DisplayName, currentUser.DisplayName);
        Assert.Equal(result.FamilyId, currentUser.FamilyId);
        Assert.Equal(result.FamilyName, currentUser.FamilyName);
    }

    [Fact]
    public async Task Logout_WhenAuthenticated_ClearsAuthenticationCookie()
    {
        // Arrange
        using var client = await CreateClientWithCsrfAsync();

        var registerResponse = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(
                Email: $"logout-{Guid.NewGuid():N}@spendmate.local",
                DisplayName: "Logout Test User",
                Password: "SecurePassword123!",
                FamilyName: "Logout Test Family"));

        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

        await RefreshCsrfTokenAsync(client);

        // Act
        var logoutResponse = await client.PostAsync(
            "/api/auth/logout",
            content: null);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);

        Assert.Contains(
            logoutResponse.Headers.GetValues("Set-Cookie"),
            cookie => cookie.Contains(
                ".SpendMate.Auth=",
                StringComparison.Ordinal));

        // Verify the session can no longer access protected endpoints.
        var protectedResponse = await client.GetAsync(
            $"/api/families/{SpendMateApiFactory.TestFamilyId}/expenses");

        var meResponse = await client.GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.Unauthorized, meResponse.StatusCode);
    }

    [Fact]
    public async Task GetMe_WithoutAuthentication_ReturnsUnauthorized()
    {
        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            });

        var response = await client.GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SetActiveFamily_WithValidMembership_UpdatesActiveFamilyAndGetMe()
    {
        // Arrange
        using var client =
            await factory.CreateAuthenticatedClientWithCsrfAsync(
                SpendMateApiFactory.TestUserId);

        var request = new SetActiveFamilyRequest(
            SpendMateApiFactory.TestSharedFamilyId);

        // Act
        var response = await client.PutAsJsonAsync(
            "/api/me/active-family",
            request);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider
                .GetRequiredService<SpendMateDbContext>();

            var user = await dbContext.Users
                .SingleAsync(x =>
                    x.Id == SpendMateApiFactory.TestUserId);

            Assert.Equal(
                SpendMateApiFactory.TestSharedFamilyId,
                user.ActiveFamilyId);
        }

        var meResponse = await client.GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);

        var currentUser =
            await meResponse.Content.ReadFromJsonAsync<AuthResponse>();

        Assert.NotNull(currentUser);

        Assert.Equal(
            SpendMateApiFactory.TestSharedFamilyId,
            currentUser.FamilyId);
    }

    [Fact]
    public async Task SetActiveFamily_WhenFamilyDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        using var client =
            await factory.CreateAuthenticatedClientWithCsrfAsync(
                SpendMateApiFactory.TestUserId);

        var request = new SetActiveFamilyRequest(Guid.NewGuid());

        // Act
        var response = await client.PutAsJsonAsync(
            "/api/me/active-family",
            request);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SetActiveFamily_WhenUserIsNotMember_ReturnsUnauthorized()
    {
        // Arrange
        using var client =
            await factory.CreateAuthenticatedClientWithCsrfAsync(
                SpendMateApiFactory.TestUserId);

        var request = new SetActiveFamilyRequest(
            SpendMateApiFactory.NonMemberFamilyId);

        // Act
        var response = await client.PutAsJsonAsync(
            "/api/me/active-family",
            request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SetActiveFamily_WithoutCsrfToken_ReturnsBadRequest()
    {
        // Arrange
        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            });

        client.DefaultRequestHeaders.Add(
            TestAuthenticationHandler.UserIdHeader,
            SpendMateApiFactory.TestUserId.ToString());

        var request = new SetActiveFamilyRequest(
            SpendMateApiFactory.TestSharedFamilyId);

        // Act
        var response = await client.PutAsJsonAsync(
            "/api/me/active-family",
            request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task RefreshCsrfTokenAsync(HttpClient client)
    {
        var csrfResponse = await client.GetAsync("/api/auth/csrf");
        csrfResponse.EnsureSuccessStatusCode();

        var csrfResult = await csrfResponse.Content
            .ReadFromJsonAsync<CsrfTokenResponse>();

        Assert.NotNull(csrfResult);
        Assert.False(string.IsNullOrWhiteSpace(csrfResult.RequestToken));

        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add(
            "X-CSRF-TOKEN",
            csrfResult.RequestToken);
    }

    private sealed record CsrfTokenResponse(string RequestToken);
}
