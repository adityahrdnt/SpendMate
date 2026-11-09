using Microsoft.Extensions.Logging;
using SpendMate.Application.Abstractions;

namespace SpendMate.Infrastructure.Email;

public sealed class DevelopmentEmailSender(
    ILogger<DevelopmentEmailSender> logger)
    : IEmailSender
{
    public Task SendAsync(
        string recipient,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Development email captured for {Recipient} with subject {Subject}.",
            recipient,
            subject);

        return Task.CompletedTask;
    }
}
