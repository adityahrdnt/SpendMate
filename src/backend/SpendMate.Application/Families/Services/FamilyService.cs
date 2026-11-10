using SpendMate.Application.Abstractions;
using SpendMate.Application.Families.Dtos;
using SpendMate.Domain.Entities;
using SpendMate.Domain.Enums;

namespace SpendMate.Application.Families.Services;

public sealed class FamilyService(
    IFamilyRepository familyRepository,
    IFamilyMembershipRepository membershipRepository,
    ICurrentUser currentUser)
{
    public async Task<IReadOnlyList<FamilyResponse>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var memberships =
            await familyRepository.GetMembershipsByUserAsync(
                currentUser.UserId,
                cancellationToken);

        return memberships
            .Select(FamilyResponse.FromEntity)
            .ToList();
    }

    public async Task<IReadOnlyList<FamilyMemberResponse>> GetMembersAsync(
        Guid familyId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var isMember = await membershipRepository.IsSharedFamilyMemberAsync(
            familyId,
            currentUser.UserId,
            cancellationToken);

        if (!isMember)
        {
            throw new UnauthorizedAccessException(
                "You are not a member of this family.");
        }

        var members = await membershipRepository.GetMembersAsync(
            familyId,
            cancellationToken);

        return members
            .Select(FamilyMemberResponse.FromEntity)
            .ToList();
    }

    public async Task UpdateMemberRoleAsync(
        Guid familyId,
        Guid targetUserId,
        UpdateFamilyMemberRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        EnsureAuthenticated();

        var role = request.Role?.Trim();

        FamilyRole newRole;

        if (string.Equals(
                role,
                nameof(FamilyRole.Owner),
                StringComparison.OrdinalIgnoreCase))
        {
            newRole = FamilyRole.Owner;
        }
        else if (string.Equals(
                     role,
                     nameof(FamilyRole.Member),
                     StringComparison.OrdinalIgnoreCase))
        {
            newRole = FamilyRole.Member;
        }
        else
        {
            throw new ArgumentException(
                "Role must be either Owner or Member.",
                nameof(request));
        }

        var result = await membershipRepository.UpdateRoleAsync(
            familyId,
            currentUser.UserId,
            targetUserId,
            newRole,
            cancellationToken);

        switch (result)
        {
            case FamilyMembershipOperationResult.Success:
                return;

            case FamilyMembershipOperationResult.FamilyNotFound:
            case FamilyMembershipOperationResult.NotSharedFamily:
            case FamilyMembershipOperationResult.TargetMembershipNotFound:
                throw new KeyNotFoundException(
                    "The family or target membership was not found.");

            case FamilyMembershipOperationResult.RequesterNotOwner:
                throw new UnauthorizedAccessException(
                    "Only family Owners can change member roles.");

            case FamilyMembershipOperationResult.LastOwner:
                throw new InvalidOperationException(
                    "A family must have at least one Owner.");

            case FamilyMembershipOperationResult.CannotChangeOwnRole:
                throw new InvalidOperationException(
                    "You cannot change your own role.");

            default:
                throw new InvalidOperationException(
                    "The membership role could not be updated.");
        }
    }

    public async Task RemoveMemberAsync(
        Guid familyId,
        Guid targetUserId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var result = await membershipRepository.RemoveMemberAsync(
            familyId,
            currentUser.UserId,
            targetUserId,
            cancellationToken);

        switch (result)
        {
            case FamilyMembershipOperationResult.Success:
                return;

            case FamilyMembershipOperationResult.FamilyNotFound:
            case FamilyMembershipOperationResult.NotSharedFamily:
            case FamilyMembershipOperationResult.TargetMembershipNotFound:
                throw new KeyNotFoundException(
                    "The family or target membership was not found.");

            case FamilyMembershipOperationResult.RequesterNotOwner:
                throw new UnauthorizedAccessException(
                    "Only family Owners can remove members.");

            case FamilyMembershipOperationResult.LastOwner:
                throw new InvalidOperationException(
                    "A family must have at least one Owner.");

            default:
                throw new InvalidOperationException(
                    "The family membership could not be removed.");
        }
    }

    public async Task<FamilyResponse> CreateAsync(
        CreateFamilyRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var name = request.Name?.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "Family name is required.",
                nameof(request));
        }

        if (name.Length > 100)
        {
            throw new ArgumentException(
                "Family name must not exceed 100 characters.",
                nameof(request));
        }

        var family = new Family
        {
            Name = name,
            Type = FamilyType.Shared,
            PersonalOwnerUserId = null
        };

        var ownerMembership = new FamilyMembership
        {
            FamilyId = family.Id,
            UserId = currentUser.UserId,
            Role = FamilyRole.Owner,
            Family = family
        };

        await familyRepository.CreateWithOwnerAsync(
            family,
            ownerMembership,
            cancellationToken);

        return FamilyResponse.FromEntity(ownerMembership);
    }

    private void EnsureAuthenticated()
    {
        if (currentUser.UserId == Guid.Empty)
        {
            throw new UnauthorizedAccessException(
                "The current user is not authenticated.");
        }
    }
}
