using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using SpendMate.Application.Auth.Dtos;
using SpendMate.Infrastructure.Data;
using SpendMate.IntegrationTests.Support;
using Xunit;

namespace SpendMate.IntegrationTests.Authentication;

public sealed class EmailVerificationEndpointsTests(
    SpendMateApiFactory factory)
    : IClassFixture<SpendMateApiFactory>
{
    [Fact]
    public async Task Register_SendsEmailVerificationEmail()
    {
        // Arrange
        var email = $"verification-{Guid.NewGuid():N}@spendmate.local";
        using var client = await CreateClientWithCsrfAsync();

        // Act
        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(
                email,
                "Verification Test User",
                "SecurePassword123!",
                "Verification Test Family"));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var sentEmail = Assert.Single(
            factory.EmailSender.SentEmails,
            x => x.Recipient == email);

        Assert.Contains("verify-email", sentEmail.HtmlBody);
        Assert.Contains("token=", sentEmail.HtmlBody);
    }

    [Fact]
    public async Task VerifyEmail_WithValidToken_ReturnsSuccess()
    {
        // Arrange
        var email = $"verify-valid-{Guid.NewGuid():N}@spendmate.local";
        using var client = await CreateClientWithCsrfAsync();

        var registerResponse = await RegisterAsync(client, email);

        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

        var sentEmail = Assert.Single(
            factory.EmailSender.SentEmails,
            x => x.Recipient == email);

        var verificationUrlMatch = Regex.Match(
            sentEmail.HtmlBody,
            @"https?://[^""'<\s]+",
            RegexOptions.IgnoreCase);

        Assert.True(verificationUrlMatch.Success);

        // Tampilkan URL tanpa token.
        var verificationUrl = verificationUrlMatch.Value;
        var uri = new Uri(verificationUrl);

        Console.WriteLine(
            $"Verification URL: {uri.GetLeftPart(UriPartial.Path)}?token=[REDACTED]");

        var token = ExtractVerificationToken(sentEmail.HtmlBody);

        var csrfCookieExists = client.DefaultRequestHeaders
            .Contains("Cookie");

        var csrfHeaderExists = client.DefaultRequestHeaders
            .Contains("X-CSRF-TOKEN");

        Assert.True(
            csrfHeaderExists,
            "CSRF request header is missing.");

        // Act
        await RefreshCsrfTokenAsync(client);

        var response = await client.PostAsJsonAsync(
            "/api/auth/verify-email",
            new VerifyEmailRequest(token));

        // Assert
        var responseBody = await response.Content.ReadAsStringAsync();

        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Status: {response.StatusCode}. Response: {responseBody}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Verify persisted user state.
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider
                .GetRequiredService<SpendMateDbContext>();

            var user = await dbContext.Users
                .SingleAsync(x => x.Email == email);

            Assert.NotNull(user.EmailVerifiedAtUtc);
            Assert.Null(user.EmailVerificationTokenHash);
            Assert.Null(user.EmailVerificationExpiresAtUtc);
        }

        await RefreshCsrfTokenAsync(client);

        // A verification token must not be reusable.
        var secondResponse = await client.PostAsJsonAsync(
            "/api/auth/verify-email",
            new VerifyEmailRequest(token));

        Assert.Equal(HttpStatusCode.BadRequest, secondResponse.StatusCode);
    }

    [Fact]
    public async Task VerifyEmail_WithInvalidToken_ReturnsBadRequest()
    {
        // Arrange
        using var client = await CreateClientWithCsrfAsync();

        // Act
        var response = await client.PostAsJsonAsync(
            "/api/auth/verify-email",
            new VerifyEmailRequest("invalid-token"));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ResendVerification_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        using var client = await CreateClientWithCsrfAsync();

        // Act
        var response = await client.PostAsync(
            "/api/auth/resend-verification",
            content: null);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ResendVerification_InvalidatesPreviousToken()
    {
        // Arrange
        var email = $"verify-resend-{Guid.NewGuid():N}@spendmate.local";
        using var client = await CreateClientWithCsrfAsync();

        var registerResponse = await RegisterAsync(client, email);

        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

        await RefreshCsrfTokenAsync(client);

        var originalEmail = Assert.Single(
            factory.EmailSender.SentEmails,
            x => x.Recipient == email);

        var originalToken = ExtractVerificationToken(
            originalEmail.HtmlBody);

        // Act
        var resendResponse = await client.PostAsync(
            "/api/auth/resend-verification",
            content: null);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, resendResponse.StatusCode);

        var emails = factory.EmailSender.SentEmails
            .Where(x => x.Recipient == email)
            .ToArray();

        Assert.Equal(2, emails.Length);

        var newToken = ExtractVerificationToken(emails[^1].HtmlBody);

        Assert.NotEqual(originalToken, newToken);

        var oldTokenResponse = await client.PostAsJsonAsync(
            "/api/auth/verify-email",
            new VerifyEmailRequest(originalToken));

        Assert.Equal(
            HttpStatusCode.BadRequest,
            oldTokenResponse.StatusCode);

        var newTokenResponse = await client.PostAsJsonAsync(
            "/api/auth/verify-email",
            new VerifyEmailRequest(newToken));

        Assert.Equal(HttpStatusCode.OK, newTokenResponse.StatusCode);
    }


    [Fact]
    public async Task VerifyEmail_WithExpiredToken_ReturnsBadRequestAndClearsToken()
    {
        // Arrange
        var email = $"verify-expired-{Guid.NewGuid():N}@spendmate.local";
        using var client = await CreateClientWithCsrfAsync();

        var registerResponse = await RegisterAsync(client, email);

        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

        var sentEmail = Assert.Single(
            factory.EmailSender.SentEmails,
            x => x.Recipient == email);

        var token = ExtractVerificationToken(sentEmail.HtmlBody);

        // Simulate token expiration in the database.
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider
                .GetRequiredService<SpendMateDbContext>();

            var user = await dbContext.Users
                .SingleAsync(x => x.Email == email);

            Assert.NotNull(user.EmailVerificationTokenHash);
            Assert.NotNull(user.EmailVerificationExpiresAtUtc);

            user.EmailVerificationExpiresAtUtc =
                DateTimeOffset.UtcNow.AddHours(-1);

            await dbContext.SaveChangesAsync();
        }

        await RefreshCsrfTokenAsync(client);

        // Act
        var response = await client.PostAsJsonAsync(
            "/api/auth/verify-email",
            new VerifyEmailRequest(token));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // Verify expired token was cleared from the database.
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider
                .GetRequiredService<SpendMateDbContext>();

            var user = await dbContext.Users
                .SingleAsync(x => x.Email == email);

            Assert.Null(user.EmailVerifiedAtUtc);
            Assert.Null(user.EmailVerificationTokenHash);
            Assert.Null(user.EmailVerificationExpiresAtUtc);
        }
    }


    private async Task<HttpResponseMessage> RegisterAsync(
        HttpClient client,
        string email)
    {
        return await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(
                email,
                "Email Verification User",
                "SecurePassword123!",
                "Email Verification Family"));
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

    private static string ExtractVerificationToken(string htmlBody)
    {
        var match = Regex.Match(
            htmlBody,
            @"verify-email\?token=([^""'<&\s]+)",
            RegexOptions.IgnoreCase);

        if (!match.Success)
        {
            throw new InvalidOperationException(
                "Verification token was not found in the email body.");
        }

        return Uri.UnescapeDataString(match.Groups[1].Value);
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
