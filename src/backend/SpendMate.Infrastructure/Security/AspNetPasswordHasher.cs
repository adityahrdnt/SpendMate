using Microsoft.AspNetCore.Identity;
using SpendMate.Application.Abstractions;
using SpendMate.Domain.Entities;

namespace SpendMate.Infrastructure.Security;

public sealed class AspNetPasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<User> _hasher = new();

    public string HashPassword(User user, string password)
    {
        return _hasher.HashPassword(user, password);
    }

    public bool VerifyPassword(
        User user,
        string passwordHash,
        string password)
    {
        var result = _hasher.VerifyHashedPassword(
            user,
            passwordHash,
            password);

        return result is PasswordVerificationResult.Success
            or PasswordVerificationResult.SuccessRehashNeeded;
    }
}
