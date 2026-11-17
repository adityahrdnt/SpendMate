
using Moq;
using SpendMate.Application.Abstractions;
using SpendMate.Application.Auth.Options;
using SpendMate.Application.Auth.Services;
using SpendMate.Domain.Entities;
using Microsoft.Extensions.Options;

namespace SpendMate.UnitTests.Auth;

public class PhoneVerificationServiceTests
{
    private readonly Mock<IPhoneOtpCryptography> _cryptography = new();
    private readonly Mock<IPhoneOtpProvider> _otpProvider = new();

    private readonly FakeTimeProvider _timeProvider = new(
        new DateTimeOffset(
            2026, 11, 17, 10, 0, 0, TimeSpan.Zero));

    private readonly FakeChallengeRepository _challengeRepository = new();

    private readonly User _user = new()
    {
        Email = "aditya@example.com",
        DisplayName = "Aditya",
        PhoneNumber = "+6281234567890"
    };

    private PhoneVerificationService CreateService(
        FakePhoneVerificationTransaction transaction)
    {
        return new PhoneVerificationService(
            transaction,
            _challengeRepository,
            _cryptography.Object,
            _otpProvider.Object,
            Options.Create(new PhoneOtpOptions
            {
                HashingSecret = "test-hashing-secret",
                EncryptionKey = "test-encryption-key",
                CodeLength = 6,
                ExpiryMinutes = 5,
                MaxAttempts = 5,
                ResendCooldownSeconds = 60
            }),
            _timeProvider);
    }

    [Fact]
    public async Task RequestOtpAsync_WhenUserHasNoActiveChallenge_CreatesChallengeAndSendsOtp()
    {
        var encryptedOtp = new EncryptedPhoneOtp(
            "encrypted-code",
            "encryption-nonce",
            "encryption-tag");

        _cryptography
            .Setup(x => x.ComputeHash(It.IsAny<string>()))
            .Returns("otp-hash");

        _cryptography
            .Setup(x => x.Encrypt(It.IsAny<string>()))
            .Returns(encryptedOtp);

        string? sentCode = null;

        _otpProvider
            .Setup(x => x.SendAsync(
                _user.PhoneNumber!,
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>(
                (_, code, _) => sentCode = code)
            .Returns(Task.CompletedTask);

        var transaction = new FakePhoneVerificationTransaction(
            _user,
            _challengeRepository);

        var service = CreateService(transaction);

        var result = await service.RequestOtpAsync(_user.Id);

        Assert.Equal(PhoneOtpRequestStatus.Sent, result.Status);
        Assert.NotNull(result.ChallengeId);
        Assert.Equal(
            _timeProvider.GetUtcNow().AddMinutes(5),
            result.ExpiresAtUtc);
        Assert.Equal(
            _timeProvider.GetUtcNow().AddSeconds(60),
            result.ResendAvailableAtUtc);

        var challenge = Assert.Single(
            _challengeRepository.Challenges);

        Assert.Equal(_user.Id, challenge.UserId);
        Assert.Equal(_user.PhoneNumber, challenge.PhoneNumber);
        Assert.Equal("otp-hash", challenge.CodeHash);
        Assert.Equal("encrypted-code", challenge.CodeCiphertext);
        Assert.Equal(0, challenge.Attempts);
        Assert.Null(challenge.ConsumedAtUtc);

        Assert.NotNull(sentCode);
        Assert.Matches(@"^\d{6}$", sentCode);

        _otpProvider.Verify(
            x => x.SendAsync(
                _user.PhoneNumber!,
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RequestOtpAsync_WhenProviderFails_ThrowsPhoneOtpDeliveryException()
    {
        var encryptedOtp = new EncryptedPhoneOtp(
            "encrypted-code",
            "encryption-nonce",
            "encryption-tag");

        _cryptography
            .Setup(x => x.ComputeHash(It.IsAny<string>()))
            .Returns("otp-hash");

        _cryptography
            .Setup(x => x.Encrypt(It.IsAny<string>()))
            .Returns(encryptedOtp);

        _otpProvider
            .Setup(x => x.SendAsync(
                _user.PhoneNumber!,
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Provider unavailable"));

        var transaction = new FakePhoneVerificationTransaction(
            _user,
            _challengeRepository);

        var service = CreateService(transaction);

        var exception = await Assert.ThrowsAsync<PhoneOtpDeliveryException>(
            () => service.RequestOtpAsync(_user.Id));

        Assert.IsType<InvalidOperationException>(exception.InnerException);

        Assert.Single(_challengeRepository.Challenges);
    }

    [Fact]
    public async Task RequestOtpAsync_WhenProviderIsCancelled_PropagatesCancellation()
    {
        var encryptedOtp = new EncryptedPhoneOtp(
            "encrypted-code",
            "encryption-nonce",
            "encryption-tag");

        _cryptography
            .Setup(x => x.ComputeHash(It.IsAny<string>()))
            .Returns("otp-hash");

        _cryptography
            .Setup(x => x.Encrypt(It.IsAny<string>()))
            .Returns(encryptedOtp);

        _otpProvider
            .Setup(x => x.SendAsync(
                _user.PhoneNumber!,
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var transaction = new FakePhoneVerificationTransaction(
            _user,
            _challengeRepository);

        var service = CreateService(transaction);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.RequestOtpAsync(_user.Id));

        Assert.Single(_challengeRepository.Challenges);
    }

    [Fact]
    public async Task RequestOtpAsync_WhenResentAfterCooldown_ReusesSameOtpAndChallenge()
    {
        string? originalCode = null;

        var encryptedOtp = new EncryptedPhoneOtp(
            "encrypted-code",
            "encryption-nonce",
            "encryption-tag");

        _cryptography
            .Setup(x => x.ComputeHash(It.IsAny<string>()))
            .Returns("otp-hash");

        _cryptography
            .Setup(x => x.Encrypt(It.IsAny<string>()))
            .Callback<string>(code => originalCode = code)
            .Returns(encryptedOtp);

        _cryptography
            .Setup(x => x.Decrypt(
                encryptedOtp.Ciphertext,
                encryptedOtp.Nonce,
                encryptedOtp.Tag))
            .Returns(() => originalCode!);

        var sentCodes = new List<string>();

        _otpProvider
            .Setup(x => x.SendAsync(
                _user.PhoneNumber!,
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>(
                (_, code, _) => sentCodes.Add(code))
            .Returns(Task.CompletedTask);

        var transaction = new FakePhoneVerificationTransaction(
            _user,
            _challengeRepository);

        var service = CreateService(transaction);

        var firstResult = await service.RequestOtpAsync(_user.Id);

        var originalExpiry = firstResult.ExpiresAtUtc;
        var originalChallengeId = firstResult.ChallengeId;

        _timeProvider.Advance(TimeSpan.FromSeconds(60));

        var secondResult = await service.RequestOtpAsync(_user.Id);

        Assert.Equal(PhoneOtpRequestStatus.Sent, firstResult.Status);
        Assert.Equal(PhoneOtpRequestStatus.Sent, secondResult.Status);

        Assert.Equal(originalChallengeId, secondResult.ChallengeId);
        Assert.Equal(originalExpiry, secondResult.ExpiresAtUtc);

        Assert.Equal(2, sentCodes.Count);
        Assert.Equal(originalCode, sentCodes[0]);
        Assert.Equal(sentCodes[0], sentCodes[1]);

        Assert.Single(_challengeRepository.Challenges);
        Assert.Equal(
            _timeProvider.GetUtcNow(),
            _challengeRepository.Challenges[0].LastSentAtUtc);
    }

    [Fact]
    public async Task RequestOtpAsync_WhenCalledBeforeCooldown_ReturnsCooldownWithoutSendingAgain()
    {
        var now = _timeProvider.GetUtcNow();

        var existingChallenge = new PhoneVerificationChallenge
        {
            UserId = _user.Id,
            PhoneNumber = _user.PhoneNumber!,
            CodeHash = "otp-hash",
            CodeCiphertext = "encrypted-code",
            EncryptionNonce = "encryption-nonce",
            EncryptionTag = "encryption-tag",
            CreatedAtUtc = now.AddSeconds(-30),
            ExpiresAtUtc = now.AddMinutes(5),
            LastSentAtUtc = now,
            Attempts = 0
        };

        _challengeRepository.Challenges.Add(existingChallenge);

        var transaction = new FakePhoneVerificationTransaction(
            _user,
            _challengeRepository);

        var service = CreateService(transaction);

        var result = await service.RequestOtpAsync(_user.Id);

        Assert.Equal(PhoneOtpRequestStatus.Cooldown, result.Status);
        Assert.Equal(existingChallenge.Id, result.ChallengeId);
        Assert.Equal(existingChallenge.ExpiresAtUtc, result.ExpiresAtUtc);
        Assert.Equal(
            now.AddSeconds(60),
            result.ResendAvailableAtUtc);

        Assert.Single(_challengeRepository.Challenges);

        _otpProvider.Verify(
            x => x.SendAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RequestOtpAsync_WhenExistingChallengeIsExpired_CreatesNewChallenge()
    {
        var now = _timeProvider.GetUtcNow();

        var expiredChallenge = new PhoneVerificationChallenge
        {
            UserId = _user.Id,
            PhoneNumber = _user.PhoneNumber!,
            CodeHash = "old-otp-hash",
            CodeCiphertext = "old-encrypted-code",
            EncryptionNonce = "old-nonce",
            EncryptionTag = "old-tag",
            CreatedAtUtc = now.AddMinutes(-10),
            ExpiresAtUtc = now.AddSeconds(-1),
            LastSentAtUtc = now.AddMinutes(-10),
            Attempts = 0
        };

        _challengeRepository.Challenges.Add(expiredChallenge);

        var encryptedOtp = new EncryptedPhoneOtp(
            "new-encrypted-code",
            "new-nonce",
            "new-tag");

        _cryptography
            .Setup(x => x.ComputeHash(It.IsAny<string>()))
            .Returns("new-otp-hash");

        _cryptography
            .Setup(x => x.Encrypt(It.IsAny<string>()))
            .Returns(encryptedOtp);

        string? sentCode = null;

        _otpProvider
            .Setup(x => x.SendAsync(
                _user.PhoneNumber!,
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>(
                (_, code, _) => sentCode = code)
            .Returns(Task.CompletedTask);

        var transaction = new FakePhoneVerificationTransaction(
            _user,
            _challengeRepository);

        var service = CreateService(transaction);

        var result = await service.RequestOtpAsync(_user.Id);

        Assert.Equal(PhoneOtpRequestStatus.Sent, result.Status);
        Assert.NotNull(result.ChallengeId);
        Assert.NotEqual(expiredChallenge.Id, result.ChallengeId);

        Assert.NotNull(expiredChallenge.ConsumedAtUtc);
        Assert.Equal(now, expiredChallenge.ConsumedAtUtc);

        Assert.Equal(2, _challengeRepository.Challenges.Count);

        var newChallenge = _challengeRepository.Challenges
            .Single(x => x.ConsumedAtUtc == null);

        Assert.Equal(result.ChallengeId, newChallenge.Id);
        Assert.Equal("new-otp-hash", newChallenge.CodeHash);
        Assert.Equal("new-encrypted-code", newChallenge.CodeCiphertext);
        Assert.Equal(_user.PhoneNumber, newChallenge.PhoneNumber);

        Assert.NotNull(sentCode);
        Assert.Matches(@"^\d{6}$", sentCode);

        _otpProvider.Verify(
            x => x.SendAsync(
                _user.PhoneNumber!,
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RequestOtpAsync_WhenMaxAttemptsReached_ReturnsMaxAttemptsReachedWithoutSending()
    {
        var now = _timeProvider.GetUtcNow();

        var existingChallenge = new PhoneVerificationChallenge
        {
            UserId = _user.Id,
            PhoneNumber = _user.PhoneNumber!,
            CodeHash = "otp-hash",
            CodeCiphertext = "encrypted-code",
            EncryptionNonce = "encryption-nonce",
            EncryptionTag = "encryption-tag",
            CreatedAtUtc = now.AddMinutes(-2),
            ExpiresAtUtc = now.AddMinutes(3),
            LastSentAtUtc = now.AddMinutes(-1),
            Attempts = 5
        };

        _challengeRepository.Challenges.Add(existingChallenge);

        var transaction = new FakePhoneVerificationTransaction(
            _user,
            _challengeRepository);

        var service = CreateService(transaction);

        var result = await service.RequestOtpAsync(_user.Id);

        Assert.Equal(
            PhoneOtpRequestStatus.MaxAttemptsReached,
            result.Status);

        Assert.Equal(existingChallenge.Id, result.ChallengeId);
        Assert.Equal(existingChallenge.ExpiresAtUtc, result.ExpiresAtUtc);
        Assert.Null(result.ResendAvailableAtUtc);

        Assert.Single(_challengeRepository.Challenges);

        _otpProvider.Verify(
            x => x.SendAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RequestOtpAsync_WhenPhoneNumberChanged_CreatesNewChallengeForNewNumber()
    {
        var now = _timeProvider.GetUtcNow();
        const string oldPhoneNumber = "+6281111111111";

        var existingChallenge = new PhoneVerificationChallenge
        {
            UserId = _user.Id,
            PhoneNumber = oldPhoneNumber,
            CodeHash = "old-otp-hash",
            CodeCiphertext = "old-encrypted-code",
            EncryptionNonce = "old-nonce",
            EncryptionTag = "old-tag",
            CreatedAtUtc = now.AddMinutes(-1),
            ExpiresAtUtc = now.AddMinutes(4),
            LastSentAtUtc = now.AddMinutes(-1),
            Attempts = 0
        };

        _challengeRepository.Challenges.Add(existingChallenge);

        var encryptedOtp = new EncryptedPhoneOtp(
            "new-encrypted-code",
            "new-nonce",
            "new-tag");

        _cryptography
            .Setup(x => x.ComputeHash(It.IsAny<string>()))
            .Returns("new-otp-hash");

        _cryptography
            .Setup(x => x.Encrypt(It.IsAny<string>()))
            .Returns(encryptedOtp);

        string? sentPhoneNumber = null;

        _otpProvider
            .Setup(x => x.SendAsync(
                _user.PhoneNumber!,
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>(
                (phoneNumber, _, _) => sentPhoneNumber = phoneNumber)
            .Returns(Task.CompletedTask);

        var transaction = new FakePhoneVerificationTransaction(
            _user,
            _challengeRepository);

        var service = CreateService(transaction);

        var result = await service.RequestOtpAsync(_user.Id);

        Assert.Equal(PhoneOtpRequestStatus.Sent, result.Status);
        Assert.NotNull(result.ChallengeId);
        Assert.NotEqual(existingChallenge.Id, result.ChallengeId);

        Assert.NotNull(existingChallenge.ConsumedAtUtc);
        Assert.Equal(now, existingChallenge.ConsumedAtUtc);

        var newChallenge = _challengeRepository.Challenges
            .Single(x => x.ConsumedAtUtc == null);

        Assert.Equal(_user.PhoneNumber, newChallenge.PhoneNumber);
        Assert.Equal("new-otp-hash", newChallenge.CodeHash);
        Assert.Equal(_user.PhoneNumber, sentPhoneNumber);

        _otpProvider.Verify(
            x => x.SendAsync(
                _user.PhoneNumber!,
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RequestOtpAsync_WhenPhoneAlreadyVerified_ReturnsAlreadyVerifiedWithoutSending()
    {
        _user.PhoneVerifiedAtUtc = _timeProvider.GetUtcNow();

        var transaction = new FakePhoneVerificationTransaction(
            _user,
            _challengeRepository);

        var service = CreateService(transaction);

        var result = await service.RequestOtpAsync(_user.Id);

        Assert.Equal(
            PhoneOtpRequestStatus.AlreadyVerified,
            result.Status);

        Assert.Null(result.ChallengeId);
        Assert.Empty(_challengeRepository.Challenges);

        _otpProvider.Verify(
            x => x.SendAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RequestOtpAsync_WhenPhoneNumberIsMissing_ReturnsPhoneNumberRequiredWithoutSending()
    {
        _user.PhoneNumber = null;

        var transaction = new FakePhoneVerificationTransaction(
            _user,
            _challengeRepository);

        var service = CreateService(transaction);

        var result = await service.RequestOtpAsync(_user.Id);

        Assert.Equal(
            PhoneOtpRequestStatus.PhoneNumberRequired,
            result.Status);

        Assert.Null(result.ChallengeId);
        Assert.Empty(_challengeRepository.Challenges);

        _otpProvider.Verify(
            x => x.SendAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RequestOtpAsync_WhenUserDoesNotExist_ReturnsUserNotFoundWithoutSending()
    {
        var transaction = new FakePhoneVerificationTransaction(
            _user,
            _challengeRepository);

        var service = CreateService(transaction);

        var nonExistingUserId = Guid.NewGuid();

        var result = await service.RequestOtpAsync(nonExistingUserId);

        Assert.Equal(
            PhoneOtpRequestStatus.UserNotFound,
            result.Status);

        Assert.Null(result.ChallengeId);
        Assert.Empty(_challengeRepository.Challenges);

        _otpProvider.Verify(
            x => x.SendAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task VerifyOtpAsync_WhenCodeIsValid_VerifiesPhoneNumber()
    {
        var now = _timeProvider.GetUtcNow();

        var challenge = new PhoneVerificationChallenge
        {
            UserId = _user.Id,
            PhoneNumber = _user.PhoneNumber!,
            CodeHash = "otp-hash",
            CodeCiphertext = "encrypted-code",
            EncryptionNonce = "encryption-nonce",
            EncryptionTag = "encryption-tag",
            CreatedAtUtc = now.AddMinutes(-1),
            ExpiresAtUtc = now.AddMinutes(4),
            LastSentAtUtc = now.AddMinutes(-1),
            Attempts = 0
        };

        _challengeRepository.Challenges.Add(challenge);

        _cryptography
            .Setup(x => x.VerifyHash("123456", "otp-hash"))
            .Returns(true);

        var transaction = new FakePhoneVerificationTransaction(
            _user,
            _challengeRepository);

        var service = CreateService(transaction);

        var result = await service.VerifyOtpAsync(
            _user.Id,
            "123456");

        Assert.Equal(
            PhoneOtpVerificationStatus.Verified,
            result.Status);

        Assert.Equal(now, _user.PhoneVerifiedAtUtc);
        Assert.Equal(now, challenge.ConsumedAtUtc);
        Assert.Equal(0, challenge.Attempts);

        _cryptography.Verify(
            x => x.VerifyHash("123456", "otp-hash"),
            Times.Once);
    }

    [Fact]
    public async Task VerifyOtpAsync_WhenCodeIsInvalid_IncrementsAttempts()
    {
        var now = _timeProvider.GetUtcNow();

        var challenge = new PhoneVerificationChallenge
        {
            UserId = _user.Id,
            PhoneNumber = _user.PhoneNumber!,
            CodeHash = "otp-hash",
            CodeCiphertext = "encrypted-code",
            EncryptionNonce = "encryption-nonce",
            EncryptionTag = "encryption-tag",
            CreatedAtUtc = now.AddMinutes(-1),
            ExpiresAtUtc = now.AddMinutes(4),
            LastSentAtUtc = now.AddMinutes(-1),
            Attempts = 0
        };

        _challengeRepository.Challenges.Add(challenge);

        _cryptography
            .Setup(x => x.VerifyHash("000000", "otp-hash"))
            .Returns(false);

        var transaction = new FakePhoneVerificationTransaction(
            _user,
            _challengeRepository);

        var service = CreateService(transaction);

        var result = await service.VerifyOtpAsync(
            _user.Id,
            "000000");

        Assert.Equal(
            PhoneOtpVerificationStatus.InvalidCode,
            result.Status);

        Assert.Equal(1, challenge.Attempts);
        Assert.Null(_user.PhoneVerifiedAtUtc);
        Assert.Null(challenge.ConsumedAtUtc);

        _cryptography.Verify(
            x => x.VerifyHash("000000", "otp-hash"),
            Times.Once);
    }

    [Theory]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("1234567")]
    [InlineData("12A456")]
    [InlineData("１２３４５６")]
    public async Task VerifyOtpAsync_WhenCodeFormatIsInvalid_IncrementsAttempts(
        string code)
    {
        var now = _timeProvider.GetUtcNow();

        var challenge = new PhoneVerificationChallenge
        {
            UserId = _user.Id,
            PhoneNumber = _user.PhoneNumber!,
            CodeHash = "otp-hash",
            CodeCiphertext = "encrypted-code",
            EncryptionNonce = "encryption-nonce",
            EncryptionTag = "encryption-tag",
            CreatedAtUtc = now.AddMinutes(-1),
            ExpiresAtUtc = now.AddMinutes(4),
            LastSentAtUtc = now.AddMinutes(-1),
            Attempts = 0
        };

        _challengeRepository.Challenges.Add(challenge);

        var transaction = new FakePhoneVerificationTransaction(
            _user,
            _challengeRepository);

        var service = CreateService(transaction);

        var result = await service.VerifyOtpAsync(
            _user.Id,
            code);

        Assert.Equal(
            PhoneOtpVerificationStatus.InvalidCode,
            result.Status);

        Assert.Equal(1, challenge.Attempts);
        Assert.Null(_user.PhoneVerifiedAtUtc);
        Assert.Null(challenge.ConsumedAtUtc);

        _cryptography.Verify(
            x => x.VerifyHash(
                It.IsAny<string>(),
                It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task VerifyOtpAsync_WhenInvalidCodeReachesMaxAttempts_ReturnsMaxAttemptsReached()
    {
        var now = _timeProvider.GetUtcNow();

        var challenge = new PhoneVerificationChallenge
        {
            UserId = _user.Id,
            PhoneNumber = _user.PhoneNumber!,
            CodeHash = "otp-hash",
            CodeCiphertext = "encrypted-code",
            EncryptionNonce = "encryption-nonce",
            EncryptionTag = "encryption-tag",
            CreatedAtUtc = now.AddMinutes(-1),
            ExpiresAtUtc = now.AddMinutes(4),
            LastSentAtUtc = now.AddMinutes(-1),
            Attempts = 4
        };

        _challengeRepository.Challenges.Add(challenge);

        _cryptography
            .Setup(x => x.VerifyHash("000000", "otp-hash"))
            .Returns(false);

        var transaction = new FakePhoneVerificationTransaction(
            _user,
            _challengeRepository);

        var service = CreateService(transaction);

        var result = await service.VerifyOtpAsync(
            _user.Id,
            "000000");

        Assert.Equal(
            PhoneOtpVerificationStatus.MaxAttemptsReached,
            result.Status);

        Assert.Equal(5, challenge.Attempts);
        Assert.Null(_user.PhoneVerifiedAtUtc);
        Assert.Null(challenge.ConsumedAtUtc);

        _cryptography.Verify(
            x => x.VerifyHash("000000", "otp-hash"),
            Times.Once);
    }

    [Fact]
    public async Task VerifyOtpAsync_WhenChallengeIsExpired_ReturnsExpired()
    {
        var now = _timeProvider.GetUtcNow();

        var challenge = new PhoneVerificationChallenge
        {
            UserId = _user.Id,
            PhoneNumber = _user.PhoneNumber!,
            CodeHash = "otp-hash",
            CodeCiphertext = "encrypted-code",
            EncryptionNonce = "encryption-nonce",
            EncryptionTag = "encryption-tag",
            CreatedAtUtc = now.AddMinutes(-10),
            ExpiresAtUtc = now.AddSeconds(-1),
            LastSentAtUtc = now.AddMinutes(-10),
            Attempts = 0
        };

        _challengeRepository.Challenges.Add(challenge);

        var transaction = new FakePhoneVerificationTransaction(
            _user,
            _challengeRepository);

        var service = CreateService(transaction);

        var result = await service.VerifyOtpAsync(
            _user.Id,
            "123456");

        Assert.Equal(
            PhoneOtpVerificationStatus.Expired,
            result.Status);

        Assert.Equal(now, challenge.ConsumedAtUtc);
        Assert.Null(_user.PhoneVerifiedAtUtc);
        Assert.Equal(0, challenge.Attempts);

        _cryptography.Verify(
            x => x.VerifyHash(
                It.IsAny<string>(),
                It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task VerifyOtpAsync_WhenChallengeDoesNotExist_ReturnsChallengeNotFound()
    {
        var transaction = new FakePhoneVerificationTransaction(
            _user,
            _challengeRepository);

        var service = CreateService(transaction);

        var result = await service.VerifyOtpAsync(
            _user.Id,
            "123456");

        Assert.Equal(
            PhoneOtpVerificationStatus.ChallengeNotFound,
            result.Status);

        Assert.Null(_user.PhoneVerifiedAtUtc);
        Assert.Empty(_challengeRepository.Challenges);

        _cryptography.Verify(
            x => x.VerifyHash(
                It.IsAny<string>(),
                It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task VerifyOtpAsync_WhenPhoneAlreadyVerified_ReturnsAlreadyVerified()
    {
        var now = _timeProvider.GetUtcNow();

        _user.PhoneVerifiedAtUtc = now;

        var challenge = new PhoneVerificationChallenge
        {
            UserId = _user.Id,
            PhoneNumber = _user.PhoneNumber!,
            CodeHash = "otp-hash",
            CodeCiphertext = "encrypted-code",
            EncryptionNonce = "encryption-nonce",
            EncryptionTag = "encryption-tag",
            CreatedAtUtc = now.AddMinutes(-1),
            ExpiresAtUtc = now.AddMinutes(4),
            LastSentAtUtc = now.AddMinutes(-1),
            Attempts = 0
        };

        _challengeRepository.Challenges.Add(challenge);

        var transaction = new FakePhoneVerificationTransaction(
            _user,
            _challengeRepository);

        var service = CreateService(transaction);

        var result = await service.VerifyOtpAsync(
            _user.Id,
            "123456");

        Assert.Equal(
            PhoneOtpVerificationStatus.AlreadyVerified,
            result.Status);

        Assert.Equal(now, _user.PhoneVerifiedAtUtc);
        Assert.Null(challenge.ConsumedAtUtc);
        Assert.Equal(0, challenge.Attempts);

        _cryptography.Verify(
            x => x.VerifyHash(
                It.IsAny<string>(),
                It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task VerifyOtpAsync_WhenUserDoesNotExist_ReturnsUserNotFound()
    {
        var transaction = new FakePhoneVerificationTransaction(
            _user,
            _challengeRepository);

        var service = CreateService(transaction);

        var nonExistingUserId = Guid.NewGuid();

        var result = await service.VerifyOtpAsync(
            nonExistingUserId,
            "123456");

        Assert.Equal(
            PhoneOtpVerificationStatus.UserNotFound,
            result.Status);

        Assert.Null(_user.PhoneVerifiedAtUtc);
        Assert.Empty(_challengeRepository.Challenges);

        _cryptography.Verify(
            x => x.VerifyHash(
                It.IsAny<string>(),
                It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task VerifyOtpAsync_WhenPhoneNumberChanged_ReturnsChallengeNotFound()
    {
        var now = _timeProvider.GetUtcNow();

        var oldPhoneNumber = "+6281111111111";

        var challenge = new PhoneVerificationChallenge
        {
            UserId = _user.Id,
            PhoneNumber = oldPhoneNumber,
            CodeHash = "otp-hash",
            CodeCiphertext = "encrypted-code",
            EncryptionNonce = "encryption-nonce",
            EncryptionTag = "encryption-tag",
            CreatedAtUtc = now.AddMinutes(-1),
            ExpiresAtUtc = now.AddMinutes(4),
            LastSentAtUtc = now.AddMinutes(-1),
            Attempts = 0
        };

        _challengeRepository.Challenges.Add(challenge);

        _cryptography
            .Setup(x => x.VerifyHash("123456", "otp-hash"))
            .Returns(true);

        var transaction = new FakePhoneVerificationTransaction(
            _user,
            _challengeRepository);

        var service = CreateService(transaction);

        var result = await service.VerifyOtpAsync(
            _user.Id,
            "123456");

        Assert.Equal(
            PhoneOtpVerificationStatus.ChallengeNotFound,
            result.Status);

        Assert.Equal(now, challenge.ConsumedAtUtc);
        Assert.Null(_user.PhoneVerifiedAtUtc);
        Assert.Equal(0, challenge.Attempts);

        _cryptography.Verify(
            x => x.VerifyHash(
                It.IsAny<string>(),
                It.IsAny<string>()),
            Times.Never);
    }

    private sealed class FakePhoneVerificationTransaction(
        User user,
        FakeChallengeRepository repository)
        : IPhoneVerificationTransaction
    {
        public async Task<TResult> ExecuteAsync<TResult>(
            Guid userId,
            Func<
                User?,
                PhoneVerificationChallenge?,
                CancellationToken,
                Task<TResult>> operation,
            CancellationToken cancellationToken = default)
        {
            var challenge = await repository.GetActiveByUserIdAsync(
                userId,
                cancellationToken);

            var currentUser = user.Id == userId
                ? user
                : null;

            return await operation(
                currentUser,
                challenge,
                cancellationToken);
        }
    }

    private sealed class FakeChallengeRepository
        : IPhoneVerificationChallengeRepository
    {
        public List<PhoneVerificationChallenge> Challenges { get; } = [];

        public Task<PhoneVerificationChallenge?> GetActiveByUserIdAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                Challenges.SingleOrDefault(x =>
                    x.UserId == userId &&
                    x.ConsumedAtUtc == null));
        }

        public Task AddAsync(
            PhoneVerificationChallenge challenge,
            CancellationToken cancellationToken = default)
        {
            Challenges.Add(challenge);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeTimeProvider(DateTimeOffset initialUtcNow)
        : TimeProvider
    {
        private DateTimeOffset _utcNow = initialUtcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan amount)
        {
            _utcNow = _utcNow.Add(amount);
        }
    }
}
