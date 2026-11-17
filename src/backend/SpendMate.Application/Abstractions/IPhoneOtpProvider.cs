namespace SpendMate.Application.Abstractions;

public interface IPhoneOtpProvider
{
    Task SendAsync(
        string phoneNumber,
        string code,
        CancellationToken cancellationToken = default);
}
