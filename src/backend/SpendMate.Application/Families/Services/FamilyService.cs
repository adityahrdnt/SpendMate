using SpendMate.Application.Abstractions;
using SpendMate.Application.Families.Dtos;
using SpendMate.Domain.Entities;
using SpendMate.Domain.Enums;

namespace SpendMate.Application.Families.Services;

public sealed class FamilyService(
    IFamilyRepository familyRepository,
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
