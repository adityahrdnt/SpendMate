using System.Globalization;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using SpendMate.Application.Abstractions;
using SpendMate.Application.Auth.Options;

namespace SpendMate.Application.Auth.Services;

public sealed class PhoneVerificationService(
    IPhoneVerificationTransaction transaction,
    IPhoneVerificationChallengeRepository challengeRepository,
    IPhoneOtpCryptography cryptography,
    IPhoneOtpProvider otpProvider,
    IOptions<PhoneOtpOptions> options,
    TimeProvider timeProvider)
{
    public async Task<PhoneOtpRequestResult> RequestOtpAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var settings = options.Value;

        var dispatch = await transaction.ExecuteAsync(
            userId,
            async (user, challenge, token) =>
            {
                var now = timeProvider.GetUtcNow();

                if (user is null)
                {
                    return new PhoneOtpDispatch(
                        new PhoneOtpRequestResult(
                            PhoneOtpRequestStatus.UserNotFound));
                }

                if (user.PhoneVerifiedAtUtc is not null)
                {
                    return new PhoneOtpDispatch(
                        new PhoneOtpRequestResult(
                            PhoneOtpRequestStatus.AlreadyVerified));
                }

                if (string.IsNullOrWhiteSpace(user.PhoneNumber))
                {
                    return new PhoneOtpDispatch(
                        new PhoneOtpRequestResult(
                            PhoneOtpRequestStatus.PhoneNumberRequired));
                }

                if (challenge is not null &&
                    challenge.ExpiresAtUtc <= now)
                {
                    challenge.ConsumedAtUtc = now;
                    challenge = null;
                }

                if (challenge is not null &&
                    challenge.PhoneNumber != user.PhoneNumber)
                {
                    challenge.ConsumedAtUtc = now;
                    challenge = null;
                }

                if (challenge is not null &&
                    challenge.Attempts >= settings.MaxAttempts)
                {
                    return new PhoneOtpDispatch(
                        new PhoneOtpRequestResult(
                            PhoneOtpRequestStatus.MaxAttemptsReached,
                            challenge.Id,
                            challenge.ExpiresAtUtc));
                }

                if (challenge is not null)
                {
                    var resendAvailableAt =
                        challenge.LastSentAtUtc.AddSeconds(
                            settings.ResendCooldownSeconds);

                    if (now < resendAvailableAt)
                    {
                        return new PhoneOtpDispatch(
                            new PhoneOtpRequestResult(
                                PhoneOtpRequestStatus.Cooldown,
                                challenge.Id,
                                challenge.ExpiresAtUtc,
                                resendAvailableAt));
                    }

                    var existingCode = cryptography.Decrypt(
                        challenge.CodeCiphertext,
                        challenge.EncryptionNonce,
                        challenge.EncryptionTag);

                    challenge.LastSentAtUtc = now;

                    return new PhoneOtpDispatch(
                        new PhoneOtpRequestResult(
                            PhoneOtpRequestStatus.Sent,
                            challenge.Id,
                            challenge.ExpiresAtUtc,
                            now.AddSeconds(
                                settings.ResendCooldownSeconds)),
                        user.PhoneNumber,
                        existingCode);
                }

                var code = GenerateCode(settings.CodeLength);
                var encryptedCode = cryptography.Encrypt(code);

                var newChallenge =
                    new Domain.Entities.PhoneVerificationChallenge
                    {
                        UserId = user.Id,
                        PhoneNumber = user.PhoneNumber,
                        CodeHash = cryptography.ComputeHash(code),
                        CodeCiphertext = encryptedCode.Ciphertext,
                        EncryptionNonce = encryptedCode.Nonce,
                        EncryptionTag = encryptedCode.Tag,
                        CreatedAtUtc = now,
                        ExpiresAtUtc = now.AddMinutes(
                            settings.ExpiryMinutes),
                        LastSentAtUtc = now,
                        Attempts = 0
                    };

                await challengeRepository.AddAsync(
                    newChallenge,
                    token);

                return new PhoneOtpDispatch(
                    new PhoneOtpRequestResult(
                        PhoneOtpRequestStatus.Sent,
                        newChallenge.Id,
                        newChallenge.ExpiresAtUtc,
                        now.AddSeconds(
                            settings.ResendCooldownSeconds)),
                    user.PhoneNumber,
                    code);
            },
            cancellationToken);

        if (dispatch.PhoneNumber is not null && dispatch.Code is not null)
        {
            try
            {
                await otpProvider.SendAsync(
                    dispatch.PhoneNumber,
                    dispatch.Code,
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new PhoneOtpDeliveryException(ex);
            }
        }

        return dispatch.Result;
    }

    public async Task<PhoneOtpVerificationResult> VerifyOtpAsync(
        Guid userId,
        string code,
        CancellationToken cancellationToken = default)
    {
        return await transaction.ExecuteAsync(
            userId,
            (user, challenge, token) =>
            {
                var now = timeProvider.GetUtcNow();

                if (user is null)
                {
                    return Task.FromResult(
                        new PhoneOtpVerificationResult(
                            PhoneOtpVerificationStatus.UserNotFound));
                }

                if (user.PhoneVerifiedAtUtc is not null)
                {
                    return Task.FromResult(
                        new PhoneOtpVerificationResult(
                            PhoneOtpVerificationStatus.AlreadyVerified));
                }

                if (challenge is null)
                {
                    return Task.FromResult(
                        new PhoneOtpVerificationResult(
                            PhoneOtpVerificationStatus.ChallengeNotFound));
                }

                if (challenge.ExpiresAtUtc <= now)
                {
                    challenge.ConsumedAtUtc = now;

                    return Task.FromResult(
                        new PhoneOtpVerificationResult(
                            PhoneOtpVerificationStatus.Expired));
                }

                if (string.IsNullOrWhiteSpace(user.PhoneNumber) ||
                    challenge.PhoneNumber != user.PhoneNumber)
                {
                    challenge.ConsumedAtUtc = now;

                    return Task.FromResult(
                        new PhoneOtpVerificationResult(
                            PhoneOtpVerificationStatus.ChallengeNotFound));
                }

                if (challenge.Attempts >= options.Value.MaxAttempts)
                {
                    return Task.FromResult(
                        new PhoneOtpVerificationResult(
                            PhoneOtpVerificationStatus.MaxAttemptsReached));
                }

                var isValidFormat =
                    !string.IsNullOrEmpty(code) &&
                    code.Length == options.Value.CodeLength &&
                    code.All(char.IsAsciiDigit);

                if (!isValidFormat ||
                    !cryptography.VerifyHash(code, challenge.CodeHash))
                {
                    challenge.Attempts++;

                    var status = challenge.Attempts >= options.Value.MaxAttempts
                        ? PhoneOtpVerificationStatus.MaxAttemptsReached
                        : PhoneOtpVerificationStatus.InvalidCode;

                    return Task.FromResult(
                        new PhoneOtpVerificationResult(status));
                }

                user.PhoneVerifiedAtUtc = now;
                challenge.ConsumedAtUtc = now;

                return Task.FromResult(
                    new PhoneOtpVerificationResult(
                        PhoneOtpVerificationStatus.Verified));
            },
            cancellationToken);
    }

    private static string GenerateCode(int length)
    {
        var number = RandomNumberGenerator.GetInt32(0, 1_000_000);

        return number.ToString(
            $"D{length}",
            CultureInfo.InvariantCulture);
    }

    private sealed record PhoneOtpDispatch(
        PhoneOtpRequestResult Result,
        string? PhoneNumber = null,
        string? Code = null);
}
