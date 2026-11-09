using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpendMate.Application.Families.Dtos;
using SpendMate.Infrastructure.Data;
using SpendMate.IntegrationTests.Support;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace SpendMate.IntegrationTests.Families;

public sealed class FamilyInvitationEndpointsTests(
    SpendMateApiFactory factory)
    : IClassFixture<SpendMateApiFactory>, IAsyncLifetime
{
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        factory.EmailSender.Clear();

        _client = await factory.CreateAuthenticatedClientWithCsrfAsync();
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task CreateInvitation_WithValidRequest_ReturnsCreatedAndSendsEmail()
    {
        // Arrange
        const string recipient = "guest@example.com";

        var request = new CreateFamilyInvitationRequest(recipient);

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/families/{SpendMateApiFactory.TestSharedFamilyId}/invitations",
            request);

        // Assert: API response
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var invitation =
            await response.Content.ReadFromJsonAsync<FamilyInvitationResponse>();

        Assert.NotNull(invitation);
        Assert.NotEqual(Guid.Empty, invitation.Id);
        Assert.Equal(
            SpendMateApiFactory.TestSharedFamilyId,
            invitation.FamilyId);
        Assert.Equal(recipient, invitation.Email);
        Assert.Equal("Pending", invitation.Status);

        Assert.InRange(
            invitation.ExpiresAtUtc - invitation.CreatedAtUtc,
            TimeSpan.FromDays(7) - TimeSpan.FromSeconds(5),
            TimeSpan.FromDays(7) + TimeSpan.FromSeconds(5));

        // Raw token and token hash must never be exposed in the API response.
        var responseBody = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("token", responseBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("tokenHash", responseBody, StringComparison.OrdinalIgnoreCase);

        // Assert: email delivery
        var email = Assert.Single(factory.EmailSender.SentEmails);

        Assert.Equal(recipient, email.Recipient);
        Assert.Contains("Invitation to join", email.Subject);
        Assert.Contains("Integration Test Shared Family", email.HtmlBody);
        Assert.Contains("/invitations/accept?token=", email.HtmlBody);
    }

    [Fact]
    public async Task CreateInvitation_WithExistingPendingInvitation_ResendsInvitation()
    {
        // Arrange
        const string recipient = "resend@example.com";

        var request = new CreateFamilyInvitationRequest(recipient);

        var endpoint =
            $"/api/families/{SpendMateApiFactory.TestSharedFamilyId}/invitations";

        var firstResponse = await _client.PostAsJsonAsync(endpoint, request);

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        var firstInvitation =
            await firstResponse.Content.ReadFromJsonAsync<FamilyInvitationResponse>();

        Assert.NotNull(firstInvitation);

        // Clear the first captured email to inspect the resend.
        factory.EmailSender.Clear();

        // Act
        var secondResponse = await _client.PostAsJsonAsync(endpoint, request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);

        var secondInvitation =
            await secondResponse.Content.ReadFromJsonAsync<FamilyInvitationResponse>();

        Assert.NotNull(secondInvitation);

        // Resend must reuse the same invitation record.
        Assert.Equal(firstInvitation.Id, secondInvitation.Id);
        Assert.Equal(firstInvitation.FamilyId, secondInvitation.FamilyId);
        Assert.Equal(firstInvitation.Email, secondInvitation.Email);
        Assert.Equal("Pending", secondInvitation.Status);

        // CreatedAtUtc remains unchanged; expiration is renewed.
        Assert.InRange(
            Math.Abs((
                firstInvitation.CreatedAtUtc -
                secondInvitation.CreatedAtUtc).TotalMilliseconds),
            0,
            1);

        Assert.True(
            secondInvitation.ExpiresAtUtc > firstInvitation.ExpiresAtUtc);

        var email = Assert.Single(factory.EmailSender.SentEmails);

        Assert.Equal(recipient, email.Recipient);
        Assert.Contains("/invitations/accept?token=", email.HtmlBody);
    }

    [Fact]
    public async Task RevokeInvitation_WithPendingInvitation_ReturnsRevoked()
    {
        // Arrange
        var request = new CreateFamilyInvitationRequest(
            "revoke@example.com");

        var createResponse = await _client.PostAsJsonAsync(
            $"/api/families/{SpendMateApiFactory.TestSharedFamilyId}/invitations",
            request);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var createdInvitation =
            await createResponse.Content.ReadFromJsonAsync<FamilyInvitationResponse>();

        Assert.NotNull(createdInvitation);

        // Act
        var revokeResponse = await _client.DeleteAsync(
            $"/api/families/{SpendMateApiFactory.TestSharedFamilyId}/invitations/{createdInvitation.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, revokeResponse.StatusCode);

        var revokedInvitation =
            await revokeResponse.Content.ReadFromJsonAsync<FamilyInvitationResponse>();

        Assert.NotNull(revokedInvitation);
        Assert.Equal(createdInvitation.Id, revokedInvitation.Id);
        Assert.Equal("Revoked", revokedInvitation.Status);
    }

    [Fact]
    public async Task CreateInvitation_AsNonOwner_ReturnsUnauthorized()
    {
        // Arrange
        using var nonOwnerClient =
            await factory.CreateAuthenticatedClientWithCsrfAsync(
                SpendMateApiFactory.SecondTestUserId);

        var request = new CreateFamilyInvitationRequest(
            "nonowner@example.com");

        // Act
        var response = await nonOwnerClient.PostAsJsonAsync(
            $"/api/families/{SpendMateApiFactory.TestSharedFamilyId}/invitations",
            request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(factory.EmailSender.SentEmails);
    }

    [Fact]
    public async Task RevokeInvitation_AsNonOwner_ReturnsUnauthorized()
    {
        // Arrange: Owner creates an invitation first.
        var request = new CreateFamilyInvitationRequest(
            "revoke-nonowner@example.com");

        var createResponse = await _client.PostAsJsonAsync(
            $"/api/families/{SpendMateApiFactory.TestSharedFamilyId}/invitations",
            request);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var invitation =
            await createResponse.Content.ReadFromJsonAsync<FamilyInvitationResponse>();

        Assert.NotNull(invitation);

        using var nonOwnerClient =
            await factory.CreateAuthenticatedClientWithCsrfAsync(
                SpendMateApiFactory.SecondTestUserId);

        // Act
        var response = await nonOwnerClient.DeleteAsync(
            $"/api/families/{SpendMateApiFactory.TestSharedFamilyId}/invitations/{invitation.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateInvitation_ForPersonalFamily_ReturnsUnauthorized()
    {
        // Arrange
        var request = new CreateFamilyInvitationRequest(
            "personal-invite@example.com");

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/families/{SpendMateApiFactory.TestFamilyId}/invitations",
            request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(factory.EmailSender.SentEmails);
    }

    [Fact]
    public async Task CreateInvitation_ForExistingMember_ReturnsConflict()
    {
        // Arrange
        var request = new CreateFamilyInvitationRequest(
            "integration-test@spendmate.local");

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/families/{SpendMateApiFactory.TestSharedFamilyId}/invitations",
            request);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Empty(factory.EmailSender.SentEmails);
    }

    [Fact]
    public async Task CreateInvitation_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            });

        // Obtain CSRF token without authenticating.
        var csrfResponse = await client.GetAsync("/api/auth/csrf");

        Assert.Equal(HttpStatusCode.OK, csrfResponse.StatusCode);

        var csrfToken = await csrfResponse.Content
            .ReadFromJsonAsync<CsrfTokenResponse>();

        Assert.NotNull(csrfToken);
        Assert.False(string.IsNullOrWhiteSpace(csrfToken.RequestToken));

        client.DefaultRequestHeaders.Add(
            "X-CSRF-TOKEN",
            csrfToken.RequestToken);

        var request = new CreateFamilyInvitationRequest(
            "anonymous@example.com");

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/families/{SpendMateApiFactory.TestSharedFamilyId}/invitations",
            request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(factory.EmailSender.SentEmails);
    }

    [Fact]
    public async Task CreateInvitation_WithoutCsrfToken_ReturnsBadRequest()
    {
        // Arrange
        using var client = factory.CreateAuthenticatedClient();

        var request = new CreateFamilyInvitationRequest(
            "no-csrf@example.com");

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/families/{SpendMateApiFactory.TestSharedFamilyId}/invitations",
            request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(factory.EmailSender.SentEmails);
    }

    [Fact]
    public async Task RevokeInvitation_WithAlreadyRevokedInvitation_ReturnsConflict()
    {
        // Arrange
        var request = new CreateFamilyInvitationRequest(
            "already-revoked@example.com");

        var createResponse = await _client.PostAsJsonAsync(
            $"/api/families/{SpendMateApiFactory.TestSharedFamilyId}/invitations",
            request);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var invitation =
            await createResponse.Content.ReadFromJsonAsync<FamilyInvitationResponse>();

        Assert.NotNull(invitation);

        var revokeEndpoint =
            $"/api/families/{SpendMateApiFactory.TestSharedFamilyId}/invitations/{invitation.Id}";

        var firstRevokeResponse = await _client.DeleteAsync(revokeEndpoint);

        Assert.Equal(HttpStatusCode.OK, firstRevokeResponse.StatusCode);

        // Act: revoke the same invitation again.
        var secondRevokeResponse = await _client.DeleteAsync(revokeEndpoint);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, secondRevokeResponse.StatusCode);
    }

    [Fact]
    public async Task RevokeInvitation_WithNonExistingId_ReturnsNotFound()
    {
        // Arrange
        var nonExistingInvitationId = Guid.NewGuid();

        // Act
        var response = await _client.DeleteAsync(
            $"/api/families/{SpendMateApiFactory.TestSharedFamilyId}/invitations/{nonExistingInvitationId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RevokeInvitation_WithExpiredInvitation_MarksAsExpired()
    {
        // Arrange
        var request = new CreateFamilyInvitationRequest(
            "expired-invitation@example.com");

        var createResponse = await _client.PostAsJsonAsync(
            $"/api/families/{SpendMateApiFactory.TestSharedFamilyId}/invitations",
            request);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var invitation =
            await createResponse.Content.ReadFromJsonAsync<FamilyInvitationResponse>();

        Assert.NotNull(invitation);

        // Act: wait is not necessary; this scenario requires an expired
        // invitation in the database, so we will adjust its expiry directly.
        await using var scope = factory.Services.CreateAsyncScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<SpendMateDbContext>();

        var storedInvitation = await dbContext.FamilyInvitations
            .SingleAsync(item => item.Id == invitation.Id);

        storedInvitation.ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1);

        await dbContext.SaveChangesAsync();

        var response = await _client.DeleteAsync(
            $"/api/families/{SpendMateApiFactory.TestSharedFamilyId}/invitations/{invitation.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var expiredInvitation =
            await response.Content.ReadFromJsonAsync<FamilyInvitationResponse>();

        Assert.NotNull(expiredInvitation);
        Assert.Equal("Expired", expiredInvitation.Status);
    }

    private sealed record CsrfTokenResponse(string RequestToken);
}
