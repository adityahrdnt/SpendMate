
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using SpendMate.Application.Abstractions;
using SpendMate.Application.Families.Dtos;
using SpendMate.Domain.Entities;
using SpendMate.Domain.Enums;

namespace SpendMate.Application.Families.Services;

public sealed class FamilyInvitationService(
    IFamilyRepository familyRepository,
    IFamilyMembershipRepository membershipRepository,
    IFamilyInvitationRepository invitationRepository,
    IUserRepository userRepository,
    ICurrentUser currentUser,
    IEmailSender emailSender,
    IInvitationLinkBuilder invitationLinkBuilder)
{
    private static readonly TimeSpan InvitationLifetime =
        TimeSpan.FromDays(7);

    public async Task<CreateFamilyInvitationResult> CreateAsync(
        Guid familyId,
        CreateFamilyInvitationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureAuthenticated();

        var family = await familyRepository
            .GetSharedFamilyOwnedByUserAsync(
                familyId,
                currentUser.UserId,
                cancellationToken);

        if (family is null)
        {
            throw new UnauthorizedAccessException(
                "The family does not exist or you are not an Owner.");
        }

        var now = DateTimeOffset.UtcNow;
        var token = GenerateToken();
        var tokenHash = HashToken(token);

        FamilyInvitation? existingInvitation;
        string? email = null;
        string? phoneNumber = null;

        switch (request.InvitationType)
        {
            case FamilyInvitationType.Email:
                if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
                {
                    throw new ArgumentException(
                        "Phone number must not be provided for an email invitation.");
                }

                email = NormalizeEmail(request.Email);
                ValidateEmail(email);

                if (await membershipRepository.IsMemberByEmailAsync(
                    familyId,
                    email,
                    cancellationToken))
                {
                    throw new InvalidOperationException(
                        "This user is already a member of the family.");
                }

                existingInvitation =
                    await invitationRepository.GetPendingByFamilyAndEmailAsync(
                        familyId,
                        email,
                        cancellationToken);

                break;

            case FamilyInvitationType.Phone:
                if (!string.IsNullOrWhiteSpace(request.Email))
                {
                    throw new ArgumentException(
                        "Email must not be provided for a phone invitation.");
                }

                phoneNumber = NormalizePhoneNumber(request.PhoneNumber);
                ValidatePhoneNumber(phoneNumber);

                var existingUser = await userRepository.GetByPhoneNumberAsync(
                    phoneNumber,
                    cancellationToken);

                if (existingUser is not null &&
                    await membershipRepository.IsMemberAsync(
                        familyId,
                        existingUser.Id,
                        cancellationToken))
                {
                    throw new InvalidOperationException(
                        "This user is already a member of the family.");
                }

                existingInvitation =
                    await invitationRepository.GetPendingByFamilyAndPhoneAsync(
                        familyId,
                        phoneNumber,
                        cancellationToken);

                break;

            default:
                throw new ArgumentException(
                    "A valid invitation type is required.",
                    nameof(request));
        }

        var wasResent = false;

        if (existingInvitation is not null &&
            existingInvitation.ExpiresAtUtc > now)
        {
            existingInvitation.TokenHash = tokenHash;
            existingInvitation.ExpiresAtUtc =
                now.Add(InvitationLifetime);

            wasResent = true;
        }
        else
        {
            if (existingInvitation is not null)
            {
                existingInvitation.Status =
                    FamilyInvitationStatus.Expired;
            }

            existingInvitation = new FamilyInvitation
            {
                FamilyId = familyId,
                InvitedByUserId = currentUser.UserId,
                InvitationType = request.InvitationType,
                Email = email,
                NormalizedEmail = email,
                PhoneNumber = phoneNumber,
                TokenHash = tokenHash,
                Status = FamilyInvitationStatus.Pending,
                CreatedAtUtc = now,
                ExpiresAtUtc = now.Add(InvitationLifetime)
            };

            await invitationRepository.AddAsync(
                existingInvitation,
                cancellationToken);
        }

        await invitationRepository.SaveChangesAsync(
            cancellationToken);

        var invitationLink = invitationLinkBuilder.Build(token);

        if (request.InvitationType == FamilyInvitationType.Email)
        {
            var emailBody = $"""
                <p>You have been invited to join <strong>{System.Net.WebUtility.HtmlEncode(family.Name)}</strong> on SpendMate.</p>
                <p><a href="{System.Net.WebUtility.HtmlEncode(invitationLink)}">Accept invitation</a></p>
                <p>This invitation expires in 7 days.</p>
                """;

            var subject = $"Invitation to join {family.Name} on SpendMate";

            await emailSender.SendAsync(
                email!,
                subject,
                emailBody,
                cancellationToken);
        }

        return new CreateFamilyInvitationResult(
            ToResponse(existingInvitation),
            invitationLink,
            wasResent);
    }

    public async Task<FamilyInvitationResponse> AcceptAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ArgumentException(
                "A valid invitation token is required.",
                nameof(token));
        }

        var tokenHash = HashToken(token);

        var invitation = await invitationRepository
            .GetByTokenHashAsync(tokenHash, cancellationToken);

        if (invitation is null)
        {
            throw new KeyNotFoundException(
                "The invitation is invalid or no longer available.");
        }

        if (invitation.Status != FamilyInvitationStatus.Pending)
        {
            throw new InvalidOperationException(
                "This invitation is no longer pending.");
        }

        var now = DateTimeOffset.UtcNow;

        if (invitation.ExpiresAtUtc <= now)
        {
            invitation.Status = FamilyInvitationStatus.Expired;

            await invitationRepository.SaveChangesAsync(
                cancellationToken);

            throw new InvalidOperationException(
                "This invitation has expired.");
        }

        var user = await userRepository.GetByIdAsync(
            currentUser.UserId,
            cancellationToken);

        if (user is null)
        {
            throw new UnauthorizedAccessException(
                "The current user was not found.");
        }

        switch (invitation.InvitationType)
        {
            case FamilyInvitationType.Email:
                if (user.EmailVerifiedAtUtc is null)
                {
                    throw new UnauthorizedAccessException(
                        "Email verification is required to accept invitations.");
                }

                var normalizedUserEmail =
                    NormalizeEmail(user.Email);

                if (!string.Equals(
                    normalizedUserEmail,
                    invitation.NormalizedEmail,
                    StringComparison.Ordinal))
                {
                    throw new UnauthorizedAccessException(
                        "The invitation email does not match the current account.");
                }

                break;

            case FamilyInvitationType.Phone:
                if (user.PhoneVerifiedAtUtc is null)
                {
                    throw new UnauthorizedAccessException(
                        "Phone verification is required to accept invitations.");
                }

                if (!string.Equals(
                    user.PhoneNumber,
                    invitation.PhoneNumber,
                    StringComparison.Ordinal))
                {
                    throw new UnauthorizedAccessException(
                        "The invitation phone number does not match the current account.");
                }

                break;

            default:
                throw new InvalidOperationException(
                    "The invitation type is invalid.");
        }

        var isAlreadyMember = await membershipRepository.IsMemberAsync(
            invitation.FamilyId,
            user.Id,
            cancellationToken);

        if (isAlreadyMember)
        {
            throw new InvalidOperationException(
                "You are already a member of this family.");
        }

        var membership = new FamilyMembership
        {
            FamilyId = invitation.FamilyId,
            UserId = user.Id,
            Role = FamilyRole.Member,
            JoinedAtUtc = now
        };

        var accepted = await membershipRepository.AddWithInvitationAsync(
            membership,
            invitation,
            now,
            cancellationToken);

        if (!accepted)
        {
            throw new InvalidOperationException(
                "This invitation is no longer available.");
        }

        return ToResponse(
            invitation,
            FamilyInvitationStatus.Accepted);
    }

    public async Task<FamilyInvitationResponse> RevokeAsync(
        Guid familyId,
        Guid invitationId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var family = await familyRepository
            .GetSharedFamilyOwnedByUserAsync(
                familyId,
                currentUser.UserId,
                cancellationToken);

        if (family is null)
        {
            throw new UnauthorizedAccessException(
                "The family does not exist or you are not an Owner.");
        }

        var invitation = await invitationRepository
            .GetByIdAndFamilyAsync(
                invitationId,
                familyId,
                cancellationToken);

        if (invitation is null)
        {
            throw new KeyNotFoundException(
                "The invitation was not found.");
        }

        if (invitation.Status != FamilyInvitationStatus.Pending)
        {
            throw new InvalidOperationException(
                "Only pending invitations can be revoked.");
        }

        var now = DateTimeOffset.UtcNow;

        if (invitation.ExpiresAtUtc <= now)
        {
            invitation.Status = FamilyInvitationStatus.Expired;
        }
        else
        {
            invitation.Status = FamilyInvitationStatus.Revoked;
            invitation.RevokedAtUtc = now;
        }

        await invitationRepository.SaveChangesAsync(
            cancellationToken);

        return ToResponse(invitation);
    }

    private void EnsureAuthenticated()
    {
        if (currentUser.UserId == Guid.Empty)
        {
            throw new UnauthorizedAccessException(
                "The current user is not authenticated.");
        }
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
                nameof(email));
        }
    }

    private static string NormalizePhoneNumber(string? phoneNumber)
    {
        return phoneNumber?.Trim() ?? string.Empty;
    }

    private static void ValidatePhoneNumber(string phoneNumber)
    {
        if (phoneNumber.Length is < 3 or > 16 ||
            phoneNumber[0] != '+' ||
            phoneNumber[1] is < '1' or > '9' ||
            phoneNumber.Skip(2).Any(
                character => character is < '0' or > '9'))
        {
            throw new ArgumentException(
                "A valid phone number in E.164 format is required.",
                nameof(phoneNumber));
        }
    }

    private static string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);

        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string HashToken(string token)
    {
        var bytes = Encoding.UTF8.GetBytes(token);
        var hash = SHA256.HashData(bytes);

        return Convert.ToHexString(hash);
    }

    private static FamilyInvitationResponse ToResponse(
        FamilyInvitation invitation,
        FamilyInvitationStatus? status = null)
    {
        return new FamilyInvitationResponse(
            invitation.Id,
            invitation.FamilyId,
            invitation.InvitationType,
            invitation.Email,
            invitation.PhoneNumber,
            (status ?? invitation.Status).ToString(),
            invitation.CreatedAtUtc,
            invitation.ExpiresAtUtc);
    }
}
