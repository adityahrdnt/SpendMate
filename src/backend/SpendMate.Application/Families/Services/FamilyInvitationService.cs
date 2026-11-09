using System.Net.Mail;
using System.Security.Cryptography;
using SpendMate.Application.Abstractions;
using SpendMate.Application.Families.Dtos;
using SpendMate.Domain.Entities;
using SpendMate.Domain.Enums;

namespace SpendMate.Application.Families.Services;

public sealed class FamilyInvitationService(
    IFamilyRepository familyRepository,
    IFamilyMembershipRepository membershipRepository,
    IFamilyInvitationRepository invitationRepository,
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

        var email = NormalizeEmail(request.Email);
        ValidateEmail(email);

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

        var isAlreadyMember =
            await membershipRepository.IsMemberByEmailAsync(
                familyId,
                email,
                cancellationToken);

        if (isAlreadyMember)
        {
            throw new InvalidOperationException(
                "This user is already a member of the family.");
        }

        var now = DateTimeOffset.UtcNow;

        var existingInvitation =
            await invitationRepository.GetPendingByFamilyAndEmailAsync(
                familyId,
                email,
                cancellationToken);

        var wasResent = false;

        var token = GenerateToken();
        var tokenHash = HashToken(token);

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

            var invitation = new FamilyInvitation
            {
                FamilyId = familyId,
                InvitedByUserId = currentUser.UserId,
                Email = email,
                NormalizedEmail = email,
                TokenHash = tokenHash,
                Status = FamilyInvitationStatus.Pending,
                CreatedAtUtc = now,
                ExpiresAtUtc = now.Add(InvitationLifetime)
            };

            await invitationRepository.AddAsync(
                invitation,
                cancellationToken);

            existingInvitation = invitation;
        }

        await invitationRepository.SaveChangesAsync(
            cancellationToken);

        var invitationLink = invitationLinkBuilder.Build(token);

        var emailBody = $"""
            <p>You have been invited to join <strong>{System.Net.WebUtility.HtmlEncode(family.Name)}</strong> on SpendMate.</p>
            <p><a href="{System.Net.WebUtility.HtmlEncode(invitationLink)}">Accept invitation</a></p>
            <p>This invitation expires in 7 days.</p>
            """;

        var subject = $"Invitation to join {family.Name} on SpendMate";

        await emailSender.SendAsync(
            email,
            subject,
            emailBody,
            cancellationToken);

        var response = new FamilyInvitationResponse(
            existingInvitation.Id,
            existingInvitation.FamilyId,
            existingInvitation.Email,
            existingInvitation.Status.ToString(),
            existingInvitation.CreatedAtUtc,
            existingInvitation.ExpiresAtUtc);

        return new CreateFamilyInvitationResult(
            response,
            token,
            wasResent);
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
        var bytes = System.Text.Encoding.UTF8.GetBytes(token);
        var hash = SHA256.HashData(bytes);

        return Convert.ToHexString(hash);
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

        return new FamilyInvitationResponse(
            invitation.Id,
            invitation.FamilyId,
            invitation.Email,
            invitation.Status.ToString(),
            invitation.CreatedAtUtc,
            invitation.ExpiresAtUtc);
    }
}
