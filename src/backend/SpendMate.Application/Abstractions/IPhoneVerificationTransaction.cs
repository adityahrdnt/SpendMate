using SpendMate.Domain.Entities;

namespace SpendMate.Application.Abstractions;

public interface IPhoneVerificationTransaction
{
    Task<TResult> ExecuteAsync<TResult>(
        Guid userId,
        Func<
            User?,
            PhoneVerificationChallenge?,
            CancellationToken,
            Task<TResult>> operation,
        CancellationToken cancellationToken = default);
}
