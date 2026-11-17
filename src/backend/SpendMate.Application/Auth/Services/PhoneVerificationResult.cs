
namespace SpendMate.Application.Auth.Services;

public enum PhoneOtpRequestStatus
{
    Sent,
    Cooldown,
    MaxAttemptsReached,
    AlreadyVerified,
    PhoneNumberRequired,
    UserNotFound
}

public sealed record PhoneOtpRequestResult(
    PhoneOtpRequestStatus Status,
    Guid? ChallengeId = null,
    DateTimeOffset? ExpiresAtUtc = null,
    DateTimeOffset? ResendAvailableAtUtc = null);

public enum PhoneOtpVerificationStatus
{
    Verified,
    InvalidCode,
    Expired,
    MaxAttemptsReached,
    ChallengeNotFound,
    AlreadyVerified,
    UserNotFound
}

public sealed record PhoneOtpVerificationResult(
    PhoneOtpVerificationStatus Status);
