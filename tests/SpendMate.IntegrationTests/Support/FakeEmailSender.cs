using System.Collections.Concurrent;
using SpendMate.Application.Abstractions;

namespace SpendMate.IntegrationTests.Support;

public sealed record CapturedEmail(
    string Recipient,
    string Subject,
    string HtmlBody);

public sealed class FakeEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<CapturedEmail> _emails = new();

    public IReadOnlyCollection<CapturedEmail> SentEmails =>
        _emails.ToArray();

    public Task SendAsync(
        string recipient,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default)
    {
        _emails.Enqueue(new CapturedEmail(
            recipient,
            subject,
            htmlBody));

        return Task.CompletedTask;
    }

    public void Clear()
    {
        while (_emails.TryDequeue(out _))
        {
        }
    }
}
