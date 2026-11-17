
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpendMate.Application.Auth.Dtos;
using SpendMate.Domain.Entities;
using SpendMate.Infrastructure.Data;
using SpendMate.IntegrationTests.Support;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace SpendMate.IntegrationTests.Authentication;

public sealed class PhoneVerificationEndpointsTests(
    SpendMateApiFactory factory)
    : IClassFixture<SpendMateApiFactory>
{
    [Fact]
    public async Task RequestPhoneVerification_SendsOtpAndVerifyPhone_VerifiesPhoneNumber()
    {
        // Arrange
        var (client, email, phoneNumber) =
            await CreateRegisteredClientAsync();

        using (client)
        {
            // Act: Request OTP
            var requestResponse = await client.PostAsync(
                "/api/auth/request-phone-verification",
                content: null);

            // Assert: OTP request
            Assert.Equal(HttpStatusCode.OK, requestResponse.StatusCode);

            var sentMessage = Assert.Single(
                factory.PhoneOtpProvider.SentMessages,
                x => x.PhoneNumber == phoneNumber);

            // Act: Verify OTP
            await RefreshCsrfTokenAsync(client);

            var verifyResponse = await client.PostAsJsonAsync(
                "/api/auth/verify-phone",
                new VerifyPhoneOtpRequest(sentMessage.Code));

            // Assert: OTP verification
            Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);

            using var scope = factory.Services.CreateScope();

            var dbContext = scope.ServiceProvider
                .GetRequiredService<SpendMateDbContext>();

            var user = await dbContext.Users
                .SingleAsync(x => x.Email == email);

            Assert.Equal(phoneNumber, user.PhoneNumber);
            Assert.NotNull(user.PhoneVerifiedAtUtc);

            var challenge = await dbContext.PhoneVerificationChallenges
                .SingleAsync(x => x.UserId == user.Id);

            Assert.NotNull(challenge.ConsumedAtUtc);
            Assert.Equal(0, challenge.Attempts);
        }
    }

    [Fact]
    public async Task RequestPhoneVerification_BeforeCooldown_ReturnsTooManyRequests()
    {
        // Arrange
        var (client, _, phoneNumber) =
            await CreateRegisteredClientAsync();

        using (client)
        {
            // Act: First request
            var firstResponse = await client.PostAsync(
                "/api/auth/request-phone-verification",
                content: null);

            Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

            // Act: Immediate second request
            await RefreshCsrfTokenAsync(client);

            var secondResponse = await client.PostAsync(
                "/api/auth/request-phone-verification",
                content: null);

            // Assert
            Assert.Equal(
                HttpStatusCode.TooManyRequests,
                secondResponse.StatusCode);

            var sentMessages = factory.PhoneOtpProvider.SentMessages
                .Where(x => x.PhoneNumber == phoneNumber)
                .ToArray();

            Assert.Single(sentMessages);
        }
    }

    [Fact]
    public async Task VerifyPhone_WithInvalidOtp_ReturnsBadRequestAndIncrementsAttempts()
    {
        // Arrange
        var (client, email, _) =
            await CreateRegisteredClientAsync();

        using (client)
        {
            var requestResponse = await client.PostAsync(
                "/api/auth/request-phone-verification",
                content: null);

            Assert.Equal(HttpStatusCode.OK, requestResponse.StatusCode);

            // Act: Submit an incorrect OTP
            await RefreshCsrfTokenAsync(client);

            var verifyResponse = await client.PostAsJsonAsync(
                "/api/auth/verify-phone",
                new VerifyPhoneOtpRequest("000000"));

            // Assert: Invalid OTP response
            Assert.Equal(
                HttpStatusCode.BadRequest,
                verifyResponse.StatusCode);

            using var scope = factory.Services.CreateScope();

            var dbContext = scope.ServiceProvider
                .GetRequiredService<SpendMateDbContext>();

            var user = await dbContext.Users
                .SingleAsync(x => x.Email == email);

            Assert.Null(user.PhoneVerifiedAtUtc);

            var challenge = await dbContext.PhoneVerificationChallenges
                .SingleAsync(x => x.UserId == user.Id);

            Assert.Equal(1, challenge.Attempts);
            Assert.Null(challenge.ConsumedAtUtc);
        }
    }

    [Fact]
    public async Task RequestPhoneVerification_AfterCooldown_ResendsSameOtp()
    {
        // Arrange
        var (client, email, phoneNumber) =
            await CreateRegisteredClientAsync();

        using (client)
        {
            var firstResponse = await client.PostAsync(
                "/api/auth/request-phone-verification",
                content: null);

            Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

            var firstMessage = Assert.Single(
                factory.PhoneOtpProvider.SentMessages,
                x => x.PhoneNumber == phoneNumber);

            Guid originalChallengeId;
            DateTimeOffset originalExpiry;

            // Simulate cooldown has elapsed
            using (var scope = factory.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider
                    .GetRequiredService<SpendMateDbContext>();

                var user = await dbContext.Users
                    .SingleAsync(x => x.Email == email);

                var challenge = await dbContext.PhoneVerificationChallenges
                    .SingleAsync(x => x.UserId == user.Id);

                originalChallengeId = challenge.Id;
                originalExpiry = challenge.ExpiresAtUtc;

                challenge.LastSentAtUtc =
                    DateTimeOffset.UtcNow.AddSeconds(-61);

                await dbContext.SaveChangesAsync();
            }

            // Act: Request OTP again
            await RefreshCsrfTokenAsync(client);

            var secondResponse = await client.PostAsync(
                "/api/auth/request-phone-verification",
                content: null);

            // Assert: Resend succeeded
            Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);

            var sentMessages = factory.PhoneOtpProvider.SentMessages
                .Where(x => x.PhoneNumber == phoneNumber)
                .ToArray();

            Assert.Equal(2, sentMessages.Length);
            Assert.Equal(firstMessage.Code, sentMessages[1].Code);

            // Assert: Existing challenge is reused
            using var assertionScope = factory.Services.CreateScope();

            var assertionDbContext = assertionScope.ServiceProvider
                .GetRequiredService<SpendMateDbContext>();

            var assertionUser = await assertionDbContext.Users
                .SingleAsync(x => x.Email == email);

            var challenges = await assertionDbContext
                .PhoneVerificationChallenges
                .Where(x => x.UserId == assertionUser.Id)
                .ToListAsync();

            var challengeAfterResend = Assert.Single(challenges);

            Assert.Equal(originalChallengeId, challengeAfterResend.Id);
            Assert.Equal(originalExpiry, challengeAfterResend.ExpiresAtUtc);
            Assert.Equal(firstMessage.Code, sentMessages[1].Code);
        }
    }

    [Fact]
    public async Task RequestPhoneVerification_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        using var client = await CreateClientWithCsrfAsync();

        // Act
        var response = await client.PostAsync(
            "/api/auth/request-phone-verification",
            content: null);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task VerifyPhone_AfterPhoneNumberChanges_ReturnsNotFoundAndConsumesChallenge()
    {
        // Arrange
        var (client, email, phoneNumber) =
            await CreateRegisteredClientAsync();

        using (client)
        {
            var requestResponse = await client.PostAsync(
                "/api/auth/request-phone-verification",
                content: null);

            Assert.Equal(HttpStatusCode.OK, requestResponse.StatusCode);

            var sentMessage = Assert.Single(
                factory.PhoneOtpProvider.SentMessages,
                x => x.PhoneNumber == phoneNumber);

            var newPhoneNumber = CreateUniquePhoneNumber();

            // Simulate user changing their phone number
            using (var scope = factory.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider
                    .GetRequiredService<SpendMateDbContext>();

                var user = await dbContext.Users
                    .SingleAsync(x => x.Email == email);

                user.PhoneNumber = newPhoneNumber;

                await dbContext.SaveChangesAsync();
            }

            // Act: Try to verify using the OTP sent to the old number
            await RefreshCsrfTokenAsync(client);

            var verifyResponse = await client.PostAsJsonAsync(
                "/api/auth/verify-phone",
                new VerifyPhoneOtpRequest(sentMessage.Code));

            // Assert
            Assert.Equal(
                HttpStatusCode.NotFound,
                verifyResponse.StatusCode);

            using var assertionScope = factory.Services.CreateScope();

            var assertionDbContext = assertionScope.ServiceProvider
                .GetRequiredService<SpendMateDbContext>();

            var assertionUser = await assertionDbContext.Users
                .SingleAsync(x => x.Email == email);

            Assert.Equal(newPhoneNumber, assertionUser.PhoneNumber);
            Assert.Null(assertionUser.PhoneVerifiedAtUtc);

            var challenge = await assertionDbContext.PhoneVerificationChallenges
                .SingleAsync(x => x.UserId == assertionUser.Id);

            Assert.NotNull(challenge.ConsumedAtUtc);
        }
    }

    private async Task<(HttpClient Client, string Email, string PhoneNumber)>
        CreateRegisteredClientAsync()
    {
        var email = $"phone-verification-{Guid.NewGuid():N}@spendmate.local";
        var phoneNumber = CreateUniquePhoneNumber();

        var client = await CreateClientWithCsrfAsync();

        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(
                email,
                phoneNumber,
                "Phone Verification User",
                "SecurePassword123!",
                "Phone Verification Family"));

        if (response.StatusCode != HttpStatusCode.OK)
        {
            var responseBody = await response.Content.ReadAsStringAsync();

            client.Dispose();

            throw new InvalidOperationException(
                $"Registration failed. Status: {response.StatusCode}. Response: {responseBody}");
        }

        await RefreshCsrfTokenAsync(client);

        return (client, email, phoneNumber);
    }

    [Fact]
    public async Task VerifyPhone_AfterFiveInvalidAttempts_ReturnsTooManyRequests()
    {
        // Arrange
        var (client, email, phoneNumber) =
            await CreateRegisteredClientAsync();

        using (client)
        {
            var requestResponse = await client.PostAsync(
                "/api/auth/request-phone-verification",
                content: null);

            Assert.Equal(HttpStatusCode.OK, requestResponse.StatusCode);

            var sentMessage = Assert.Single(
                factory.PhoneOtpProvider.SentMessages,
                x => x.PhoneNumber == phoneNumber);

            var invalidCode = sentMessage.Code == "000000"
                ? "000001"
                : "000000";

            // Act: Submit five incorrect OTP attempts
            for (var attempt = 1; attempt <= 5; attempt++)
            {
                await RefreshCsrfTokenAsync(client);

                var verifyResponse = await client.PostAsJsonAsync(
                    "/api/auth/verify-phone",
                    new VerifyPhoneOtpRequest(invalidCode));

                var expectedStatus = attempt < 5
                    ? HttpStatusCode.BadRequest
                    : HttpStatusCode.TooManyRequests;

                Assert.Equal(expectedStatus, verifyResponse.StatusCode);
            }

            // Assert: Further verification attempts are blocked
            await RefreshCsrfTokenAsync(client);

            var sixthResponse = await client.PostAsJsonAsync(
                "/api/auth/verify-phone",
                new VerifyPhoneOtpRequest(invalidCode));

            Assert.Equal(
                HttpStatusCode.TooManyRequests,
                sixthResponse.StatusCode);

            // Assert: Requesting another OTP is also blocked
            await RefreshCsrfTokenAsync(client);

            var resendResponse = await client.PostAsync(
                "/api/auth/request-phone-verification",
                content: null);

            Assert.Equal(
                HttpStatusCode.TooManyRequests,
                resendResponse.StatusCode);

            using var scope = factory.Services.CreateScope();

            var dbContext = scope.ServiceProvider
                .GetRequiredService<SpendMateDbContext>();

            var user = await dbContext.Users
                .SingleAsync(x => x.Email == email);

            Assert.Null(user.PhoneVerifiedAtUtc);

            var challenge = await dbContext.PhoneVerificationChallenges
                .SingleAsync(x => x.UserId == user.Id);

            Assert.Equal(5, challenge.Attempts);
            Assert.Null(challenge.ConsumedAtUtc);
        }
    }

    [Fact]
    public async Task VerifyPhone_WithExpiredOtp_ReturnsGoneAndConsumesChallenge()
    {
        // Arrange
        var (client, email, phoneNumber) =
            await CreateRegisteredClientAsync();

        using (client)
        {
            var requestResponse = await client.PostAsync(
                "/api/auth/request-phone-verification",
                content: null);

            Assert.Equal(HttpStatusCode.OK, requestResponse.StatusCode);

            using (var scope = factory.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider
                    .GetRequiredService<SpendMateDbContext>();

                var user = await dbContext.Users
                    .SingleAsync(x => x.Email == email);

                var challenge = await dbContext.PhoneVerificationChallenges
                    .SingleAsync(x => x.UserId == user.Id);

                challenge.ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1);

                await dbContext.SaveChangesAsync();
            }

            // Act
            await RefreshCsrfTokenAsync(client);

            var verifyResponse = await client.PostAsJsonAsync(
                "/api/auth/verify-phone",
                new VerifyPhoneOtpRequest("123456"));

            // Assert
            Assert.Equal(
                HttpStatusCode.Gone,
                verifyResponse.StatusCode);

            using var assertionScope = factory.Services.CreateScope();

            var assertionDbContext = assertionScope.ServiceProvider
                .GetRequiredService<SpendMateDbContext>();

            var assertionUser = await assertionDbContext.Users
                .SingleAsync(x => x.Email == email);

            Assert.Null(assertionUser.PhoneVerifiedAtUtc);

            var assertionChallenge =
                await assertionDbContext.PhoneVerificationChallenges
                    .SingleAsync(x => x.UserId == assertionUser.Id);

            Assert.NotNull(assertionChallenge.ConsumedAtUtc);
            Assert.Equal(0, assertionChallenge.Attempts);
        }
    }

    private async Task<HttpClient> CreateClientWithCsrfAsync()
    {
        var client = factory.CreateClient(
            new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            });

        await RefreshCsrfTokenAsync(client);

        return client;
    }

    private static async Task RefreshCsrfTokenAsync(HttpClient client)
    {
        var csrfResponse = await client.GetAsync("/api/auth/csrf");
        csrfResponse.EnsureSuccessStatusCode();

        var csrfResult = await csrfResponse.Content
            .ReadFromJsonAsync<CsrfTokenResponse>();

        if (string.IsNullOrWhiteSpace(csrfResult?.RequestToken))
        {
            throw new InvalidOperationException(
                "Failed to obtain CSRF token.");
        }

        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add(
            "X-CSRF-TOKEN",
            csrfResult.RequestToken);
    }

    private static string CreateUniquePhoneNumber()
    {
        return $"+628{Random.Shared.Next(100_000_000, 999_999_999)}";
    }

    private sealed record CsrfTokenResponse(string RequestToken);
}
