using System.Security.Cryptography;
using System.Text;
using System.Net;
using SpendMate.Application.Abstractions;

namespace SpendMate.Application.Auth.Services;

public sealed class EmailVerificationService(
    IUserRepository userRepository,
    IEmailSender emailSender,
    IEmailVerificationLinkBuilder linkBuilder,
    TimeProvider timeProvider)
{
    private static readonly TimeSpan TokenLifetime =
        TimeSpan.FromHours(24);

    public async Task SendVerificationAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByIdAsync(
            userId,
            cancellationToken);

        if (user is null)
        {
            throw new InvalidOperationException(
                "User account was not found.");
        }

        if (user.EmailVerifiedAtUtc is not null)
        {
            return;
        }

        await CreateAndSendAsync(user, cancellationToken);
    }

    public async Task<bool> ResendVerificationAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByIdAsync(
            userId,
            cancellationToken);

        if (user is null || user.EmailVerifiedAtUtc is not null)
        {
            return false;
        }

        await CreateAndSendAsync(user, cancellationToken);

        return true;
    }

    public async Task<bool> VerifyAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var tokenHash = HashToken(token);

        var user = await userRepository
            .GetByEmailVerificationTokenHashAsync(
                tokenHash,
                cancellationToken);

        if (user is null)
        {
            return false;
        }

        if (user.EmailVerifiedAtUtc is not null)
        {
            return false;
        }

        var now = timeProvider.GetUtcNow();

        if (user.EmailVerificationExpiresAtUtc is not { } expiresAt ||
            expiresAt <= now)
        {
            ClearVerificationToken(user);

            await userRepository.SaveChangesAsync(
                cancellationToken);

            return false;
        }

        user.EmailVerifiedAtUtc = now;

        ClearVerificationToken(user);

        await userRepository.SaveChangesAsync(
            cancellationToken);

        return true;
    }

    private async Task CreateAndSendAsync(
        Domain.Entities.User user,
        CancellationToken cancellationToken)
    {
        var token = GenerateToken();

        user.EmailVerificationTokenHash = HashToken(token);
        user.EmailVerificationExpiresAtUtc =
            timeProvider.GetUtcNow().Add(TokenLifetime);

        await userRepository.SaveChangesAsync(cancellationToken);

        var verificationUrl = linkBuilder.Build(token);

        var encodedUrl = WebUtility.HtmlEncode(verificationUrl);
        var encodedName = WebUtility.HtmlEncode(user.DisplayName);

        var htmlBody = $"""
            <p>Hello {encodedName},</p>
            <p>Please verify your SpendMate email address
            by clicking the link below:</p>
            <p><a href="{encodedUrl}">Verify Email Address</a></p>
            <p>This link expires in 24 hours.</p>
            <p>If you did not create this account, you can ignore this email.</p>
            """;

        await emailSender.SendAsync(
            user.Email,
            "Verify your SpendMate email address",
            htmlBody,
            cancellationToken);
    }

    private static string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);

        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string HashToken(string token)
    {
        var bytes = Encoding.UTF8.GetBytes(token);
        var hash = SHA256.HashData(bytes);

        return Convert.ToHexString(hash);
    }

    private static void ClearVerificationToken(
        Domain.Entities.User user)
    {
        user.EmailVerificationTokenHash = null;
        user.EmailVerificationExpiresAtUtc = null;
    }
}
