
using Moq;
using SpendMate.Application.Abstractions;
using SpendMate.Application.Auth.Services;
using SpendMate.Domain.Entities;

namespace SpendMate.UnitTests.Auth;

public class EmailVerificationServiceTests
{
    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IEmailSender> _emailSender = new();
    private readonly Mock<IEmailVerificationLinkBuilder> _linkBuilder = new();

    private readonly FakeTimeProvider _timeProvider = new(
        new DateTimeOffset(2026, 11, 9, 10, 0, 0, TimeSpan.Zero));

    private EmailVerificationService CreateService()
    {
        return new EmailVerificationService(
            _userRepository.Object,
            _emailSender.Object,
            _linkBuilder.Object,
            _timeProvider);
    }

    private static User CreateUser()
    {
        return new User
        {
            Email = "aditya@example.com",
            DisplayName = "Aditya"
        };
    }

    [Fact]
    public async Task SendVerificationAsync_WhenUserIsUnverified_CreatesTokenAndSendsEmail()
    {
        var user = CreateUser();
        string? generatedToken = null;

        _userRepository
            .Setup(x => x.GetByIdAsync(
                user.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _linkBuilder
            .Setup(x => x.Build(It.IsAny<string>()))
            .Callback<string>(token => generatedToken = token)
            .Returns("https://example.com/verify-email");

        _emailSender
            .Setup(x => x.SendAsync(
                user.Email,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = CreateService();

        await service.SendVerificationAsync(user.Id);

        Assert.NotNull(generatedToken);
        Assert.NotEqual(generatedToken, user.EmailVerificationTokenHash);
        Assert.Equal(64, user.EmailVerificationTokenHash?.Length);
        Assert.Equal(
            _timeProvider.GetUtcNow().AddHours(24),
            user.EmailVerificationExpiresAtUtc);

        _linkBuilder.Verify(
            x => x.Build(generatedToken),
            Times.Once);

        _emailSender.Verify(
            x => x.SendAsync(
                user.Email,
                "Verify your SpendMate email address",
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _userRepository.Verify(
            x => x.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SendVerificationAsync_WhenUserIsAlreadyVerified_DoesNotSendEmail()
    {
        var user = CreateUser();
        user.EmailVerifiedAtUtc = _timeProvider.GetUtcNow();

        _userRepository
            .Setup(x => x.GetByIdAsync(
                user.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var service = CreateService();

        await service.SendVerificationAsync(user.Id);

        _emailSender.Verify(
            x => x.SendAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        _userRepository.Verify(
            x => x.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ResendVerificationAsync_WhenUserIsUnverified_GeneratesNewToken()
    {
        var user = CreateUser();
        user.EmailVerificationTokenHash = "old-token-hash";
        user.EmailVerificationExpiresAtUtc =
            _timeProvider.GetUtcNow().AddHours(12);

        string? generatedToken = null;

        _userRepository
            .Setup(x => x.GetByIdAsync(
                user.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _linkBuilder
            .Setup(x => x.Build(It.IsAny<string>()))
            .Callback<string>(token => generatedToken = token)
            .Returns("https://example.com/verify-email");

        _emailSender
            .Setup(x => x.SendAsync(
                user.Email,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = CreateService();

        var result = await service.ResendVerificationAsync(user.Id);

        Assert.True(result);
        Assert.NotNull(generatedToken);
        Assert.NotEqual("old-token-hash", user.EmailVerificationTokenHash);
        Assert.Equal(64, user.EmailVerificationTokenHash?.Length);
        Assert.Equal(
            _timeProvider.GetUtcNow().AddHours(24),
            user.EmailVerificationExpiresAtUtc);

        _emailSender.Verify(
            x => x.SendAsync(
                user.Email,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ResendVerificationAsync_WhenUserIsAlreadyVerified_ReturnsFalse()
    {
        var user = CreateUser();
        user.EmailVerifiedAtUtc = _timeProvider.GetUtcNow();

        _userRepository
            .Setup(x => x.GetByIdAsync(
                user.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var service = CreateService();

        var result = await service.ResendVerificationAsync(user.Id);

        Assert.False(result);

        _emailSender.Verify(
            x => x.SendAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task VerifyAsync_WhenTokenIsValid_VerifiesUserAndClearsToken()
    {
        var user = CreateUser();
        const string token = "valid-verification-token";

        var tokenHash = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(token)));

        user.EmailVerificationTokenHash = tokenHash;
        user.EmailVerificationExpiresAtUtc =
            _timeProvider.GetUtcNow().AddHours(24);

        _userRepository
            .Setup(x => x.GetByEmailVerificationTokenHashAsync(
                tokenHash,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var service = CreateService();

        var result = await service.VerifyAsync(token);

        Assert.True(result);
        Assert.Equal(_timeProvider.GetUtcNow(), user.EmailVerifiedAtUtc);
        Assert.Null(user.EmailVerificationTokenHash);
        Assert.Null(user.EmailVerificationExpiresAtUtc);

        _userRepository.Verify(
            x => x.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task VerifyAsync_WhenTokenIsExpired_ReturnsFalseAndClearsToken()
    {
        var user = CreateUser();
        const string token = "expired-verification-token";

        var tokenHash = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(token)));

        user.EmailVerificationTokenHash = tokenHash;
        user.EmailVerificationExpiresAtUtc =
            _timeProvider.GetUtcNow().AddHours(-1);

        _userRepository
            .Setup(x => x.GetByEmailVerificationTokenHashAsync(
                tokenHash,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var service = CreateService();

        var result = await service.VerifyAsync(token);

        Assert.False(result);
        Assert.Null(user.EmailVerificationTokenHash);
        Assert.Null(user.EmailVerificationExpiresAtUtc);
        Assert.Null(user.EmailVerifiedAtUtc);

        _userRepository.Verify(
            x => x.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task VerifyAsync_WhenTokenDoesNotExist_ReturnsFalse()
    {
        _userRepository
            .Setup(x => x.GetByEmailVerificationTokenHashAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var service = CreateService();

        var result = await service.VerifyAsync("invalid-token");

        Assert.False(result);

        _userRepository.Verify(
            x => x.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private sealed class FakeTimeProvider(DateTimeOffset utcNow)
        : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
