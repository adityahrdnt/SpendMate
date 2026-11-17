using System.Collections.Concurrent;
using SpendMate.Application.Abstractions;

namespace SpendMate.IntegrationTests.Support;

public sealed record CapturedPhoneOtp(
    string PhoneNumber,
    string Code);

public sealed class FakePhoneOtpProvider : IPhoneOtpProvider
{
    private readonly ConcurrentQueue<CapturedPhoneOtp> _messages = new();

    public IReadOnlyCollection<CapturedPhoneOtp> SentMessages =>
        _messages.ToArray();

    public Task SendAsync(
        string phoneNumber,
        string code,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _messages.Enqueue(new CapturedPhoneOtp(
            phoneNumber,
            code));

        return Task.CompletedTask;
    }

    public void Clear()
    {
        while (_messages.TryDequeue(out _))
        {
        }
    }
}
