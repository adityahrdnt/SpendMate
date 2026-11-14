using System.Net.Mail;
using SpendMate.Application.Abstractions;
using SpendMate.Application.Auth.Dtos;
using SpendMate.Domain.Entities;
using SpendMate.Domain.Enums;

namespace SpendMate.Application.Auth.Services;

public sealed class AuthService(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher)
{
    public async Task<AuthResponse> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var email = NormalizeEmail(request.Email);
        var displayName = NormalizeRequiredText(
            request.DisplayName,
            100,
            nameof(request.DisplayName));

        var familyName = NormalizeRequiredText(
            request.FamilyName,
            100,
            nameof(request.FamilyName));

        ValidateEmail(email);
        ValidatePassword(request.Password);

        var existingUser = await userRepository.GetByEmailAsync(
            email,
            cancellationToken);

        if (existingUser is not null)
        {
            throw new InvalidOperationException(
                "An account with this email already exists.");
        }

        var user = new User
        {
            Email = email,
            DisplayName = displayName
        };

        user.PasswordHash = passwordHasher.HashPassword(
            user,
            request.Password);

        var family = new Family
        {
            Name = familyName,
            Type = FamilyType.Personal,
            PersonalOwnerUserId = user.Id
        };

        var membership = new FamilyMembership
        {
            User = user,
            Family = family,
            Role = FamilyRole.Owner
        };

        user.Memberships.Add(membership);
        family.Memberships.Add(membership);

        await userRepository.AddWithActiveFamilyAsync(
            user,
            family.Id,
            cancellationToken);

        return new AuthResponse(
            user.Id,
            user.Email,
            user.DisplayName,
            family.Id,
            family.Name);
    }

    public async Task<AuthResponse> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var email = NormalizeEmail(request.Email);

        if (string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            throw new UnauthorizedAccessException(
                "Invalid email or password.");
        }

        var user = await userRepository.GetByEmailAsync(
            email,
            cancellationToken);

        if (user?.PasswordHash is not { } passwordHash ||
            !passwordHasher.VerifyPassword(
                user,
                passwordHash,
                request.Password))
        {
            throw new UnauthorizedAccessException(
                "Invalid email or password.");
        }

        var membership = user.Memberships
            .SingleOrDefault(x => x.FamilyId == user.ActiveFamilyId);

        if (membership is null)
        {
            membership = user.Memberships
                .SingleOrDefault(x =>
                    x.Family.Type == FamilyType.Personal &&
                    x.Family.PersonalOwnerUserId == user.Id);

            if (membership is null)
            {
                throw new UnauthorizedAccessException(
                    "The user does not belong to a family.");
            }

            user.ActiveFamilyId = membership.FamilyId;

            await userRepository.SaveChangesAsync(cancellationToken);
        }

        return new AuthResponse(
            user.Id,
            user.Email,
            user.DisplayName,
            membership.FamilyId,
            membership.Family.Name);
    }

    public async Task<AuthResponse> GetCurrentUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByIdWithMembershipsAsync(
            userId,
            cancellationToken);

        if (user is null)
        {
            throw new UnauthorizedAccessException(
                "User not found.");
        }

        var membership = user.Memberships
            .SingleOrDefault(x => x.FamilyId == user.ActiveFamilyId);

        if (membership is null)
        {
            membership = user.Memberships
                .SingleOrDefault(x =>
                    x.Family.Type == FamilyType.Personal &&
                    x.Family.PersonalOwnerUserId == user.Id);

            if (membership is null)
            {
                throw new UnauthorizedAccessException(
                    "The user does not belong to a family.");
            }

            user.ActiveFamilyId = membership.FamilyId;

            await userRepository.SaveChangesAsync(cancellationToken);
        }

        return new AuthResponse(
            user.Id,
            user.Email,
            user.DisplayName,
            membership.FamilyId,
            membership.Family.Name);
    }

    private static string NormalizeEmail(string? email)
    {
        return email?.Trim().ToLowerInvariant() ?? string.Empty;
    }

    private static void ValidateEmail(string email)
    {
        if (email.Length is 0 or > 254 ||
            !MailAddress.TryCreate(email, out var address) ||
            !string.Equals(
                address.Address,
                email,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "A valid email address is required.",
                "Email");
        }
    }

    private static string NormalizeRequiredText(
        string? value,
        int maxLength,
        string parameterName)
    {
        var normalized = value?.Trim();

        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException(
                $"{parameterName} is required.",
                parameterName);
        }

        if (normalized.Length > maxLength)
        {
            throw new ArgumentException(
                $"{parameterName} must not exceed {maxLength} characters.",
                parameterName);
        }

        return normalized;
    }

    private static void ValidatePassword(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < 8)
        {
            throw new ArgumentException(
                "Password must contain at least 8 characters.",
                "Password");
        }
    }
}
