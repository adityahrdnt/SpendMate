using SpendMate.Application.Abstractions;

namespace SpendMate.Infrastructure.PhoneVerification;

public sealed class UnavailablePhoneOtpProvider : IPhoneOtpProvider
{
    public Task SendAsync(
        string phoneNumber,
        string code,
        CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException(
            "Phone OTP delivery is not configured for this environment.");
    }
}
