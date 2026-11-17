using Microsoft.Extensions.Logging;
using SpendMate.Application.Abstractions;

namespace SpendMate.Infrastructure.PhoneVerification;

public sealed class DevelopmentPhoneOtpProvider(
    ILogger<DevelopmentPhoneOtpProvider> logger)
    : IPhoneOtpProvider
{
    public Task SendAsync(
        string phoneNumber,
        string code,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        logger.LogInformation(
            "Development phone OTP for {PhoneNumber}: {Code}",
            phoneNumber,
            code);

        return Task.CompletedTask;
    }
}
