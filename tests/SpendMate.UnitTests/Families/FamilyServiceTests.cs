using Moq;
using SpendMate.Application.Abstractions;
using SpendMate.Application.Families.Dtos;
using SpendMate.Application.Families.Services;
using SpendMate.Domain.Entities;
using SpendMate.Domain.Enums;

namespace SpendMate.UnitTests.Families;

public class FamilyServiceTests
{
    private readonly Mock<IFamilyRepository> _familyRepository = new();
    private readonly Mock<IFamilyMembershipRepository> _membershipRepository = new();
    private readonly Mock<ICurrentUser> _currentUser = new();

    private readonly Guid _userId = Guid.NewGuid();

    private FamilyService CreateService()
    {
        _currentUser.SetupGet(x => x.UserId).Returns(_userId);

        return new FamilyService(
            _familyRepository.Object,
            _membershipRepository.Object,
            _currentUser.Object);
    }

    [Fact]
    public async Task GetAllAsync_WhenUserHasMemberships_ReturnsFamiliesWithRoles()
    {
        var personalFamily = new Family
        {
            Name = "Personal Family",
            Type = FamilyType.Personal,
            PersonalOwnerUserId = _userId
        };

        var sharedFamily = new Family
        {
            Name = "Shared Family",
            Type = FamilyType.Shared
        };

        var memberships = new List<FamilyMembership>
        {
            new()
            {
                FamilyId = personalFamily.Id,
                UserId = _userId,
                Role = FamilyRole.Owner,
                Family = personalFamily
            },
            new()
            {
                FamilyId = sharedFamily.Id,
                UserId = _userId,
                Role = FamilyRole.Member,
                Family = sharedFamily
            }
        };

        _familyRepository
            .Setup(x => x.GetMembershipsByUserAsync(
                _userId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(memberships);

        var service = CreateService();

        var result = await service.GetAllAsync();

        Assert.Equal(2, result.Count);

        Assert.Equal("Personal Family", result[0].Name);
        Assert.Equal("Personal", result[0].Type);
        Assert.Equal("Owner", result[0].Role);

        Assert.Equal("Shared Family", result[1].Name);
        Assert.Equal("Shared", result[1].Type);
        Assert.Equal("Member", result[1].Role);

        _familyRepository.Verify(
            x => x.GetMembershipsByUserAsync(
                _userId,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetAllAsync_WhenUserHasNoMemberships_ReturnsEmptyList()
    {
        _familyRepository
            .Setup(x => x.GetMembershipsByUserAsync(
                _userId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FamilyMembership>());

        var service = CreateService();

        var result = await service.GetAllAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task CreateAsync_WhenRequestIsValid_CreatesSharedFamilyWithOwner()
    {
        var service = CreateService();

        _familyRepository
            .Setup(x => x.CreateWithOwnerAsync(
                It.IsAny<Family>(),
                It.IsAny<FamilyMembership>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Family family, FamilyMembership _, CancellationToken _) =>
                family);

        var result = await service.CreateAsync(
            new CreateFamilyRequest("  Herdianto Family  "));

        Assert.Equal("Herdianto Family", result.Name);
        Assert.Equal("Shared", result.Type);
        Assert.Equal("Owner", result.Role);

        _familyRepository.Verify(
            x => x.CreateWithOwnerAsync(
                It.Is<Family>(family =>
                    family.Name == "Herdianto Family" &&
                    family.Type == FamilyType.Shared &&
                    family.PersonalOwnerUserId == null),
                It.Is<FamilyMembership>(membership =>
                    membership.UserId == _userId &&
                    membership.Role == FamilyRole.Owner &&
                    membership.FamilyId == membership.Family.Id),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateAsync_WhenNameIsEmpty_ThrowsArgumentException(
        string name)
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAsync(new CreateFamilyRequest(name)));

        _familyRepository.Verify(
            x => x.CreateWithOwnerAsync(
                It.IsAny<Family>(),
                It.IsAny<FamilyMembership>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WhenNameExceeds100Characters_ThrowsArgumentException()
    {
        var service = CreateService();
        var name = new string('A', 101);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAsync(new CreateFamilyRequest(name)));

        _familyRepository.Verify(
            x => x.CreateWithOwnerAsync(
                It.IsAny<Family>(),
                It.IsAny<FamilyMembership>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WhenUserIsUnauthenticated_ThrowsUnauthorizedAccessException()
    {
        var service = CreateService();

        _currentUser.SetupGet(x => x.UserId).Returns(Guid.Empty);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.CreateAsync(new CreateFamilyRequest("Test Family")));

        _familyRepository.Verify(
            x => x.CreateWithOwnerAsync(
                It.IsAny<Family>(),
                It.IsAny<FamilyMembership>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData("Owner", FamilyRole.Owner)]
    [InlineData("owner", FamilyRole.Owner)]
    [InlineData("Member", FamilyRole.Member)]
    [InlineData("member", FamilyRole.Member)]
    public async Task UpdateMemberRoleAsync_WhenRoleIsValid_UpdatesRole(
    string role,
    FamilyRole expectedRole)
    {
        var familyId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        _membershipRepository
            .Setup(x => x.UpdateRoleAsync(
                familyId,
                _userId,
                targetUserId,
                expectedRole,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(FamilyMembershipOperationResult.Success);

        var service = CreateService();

        await service.UpdateMemberRoleAsync(
            familyId,
            targetUserId,
            new UpdateFamilyMemberRoleRequest(role));

        _membershipRepository.Verify(
            x => x.UpdateRoleAsync(
                familyId,
                _userId,
                targetUserId,
                expectedRole,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Admin")]
    [InlineData("0")]
    [InlineData("3")]
    public async Task UpdateMemberRoleAsync_WhenRoleIsInvalid_ThrowsArgumentException(
    string role)
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpdateMemberRoleAsync(
                Guid.NewGuid(),
                Guid.NewGuid(),
                new UpdateFamilyMemberRoleRequest(role)));

        _membershipRepository.Verify(
            x => x.UpdateRoleAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<FamilyRole>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateMemberRoleAsync_WhenLastOwner_ThrowsInvalidOperationException()
    {
        _membershipRepository
            .Setup(x => x.UpdateRoleAsync(
                It.IsAny<Guid>(),
                _userId,
                It.IsAny<Guid>(),
                It.IsAny<FamilyRole>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(FamilyMembershipOperationResult.LastOwner);

        var service = CreateService();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateMemberRoleAsync(
                Guid.NewGuid(),
                Guid.NewGuid(),
                new UpdateFamilyMemberRoleRequest("Member")));
    }

    [Fact]
    public async Task RemoveMemberAsync_WhenRepositoryReturnsSuccess_CompletesSuccessfully()
    {
        var familyId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        _membershipRepository
            .Setup(x => x.RemoveMemberAsync(
                familyId,
                _userId,
                targetUserId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(FamilyMembershipOperationResult.Success);

        var service = CreateService();

        await service.RemoveMemberAsync(familyId, targetUserId);

        _membershipRepository.Verify(
            x => x.RemoveMemberAsync(
                familyId,
                _userId,
                targetUserId,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RemoveMemberAsync_WhenLastOwner_ThrowsInvalidOperationException()
    {
        _membershipRepository
            .Setup(x => x.RemoveMemberAsync(
                It.IsAny<Guid>(),
                _userId,
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(FamilyMembershipOperationResult.LastOwner);

        var service = CreateService();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RemoveMemberAsync(Guid.NewGuid(), Guid.NewGuid()));
    }

    [Fact]
    public async Task RemoveMemberAsync_WhenRequesterIsNotOwner_ThrowsUnauthorizedAccessException()
    {
        _membershipRepository
            .Setup(x => x.RemoveMemberAsync(
                It.IsAny<Guid>(),
                _userId,
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(FamilyMembershipOperationResult.RequesterNotOwner);

        var service = CreateService();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.RemoveMemberAsync(Guid.NewGuid(), Guid.NewGuid()));
    }

    [Fact]
    public async Task GetMembersAsync_WhenUserIsSharedFamilyMember_ReturnsMembers()
    {
        var familyId = Guid.NewGuid();

        var memberships = new List<FamilyMembership>
    {
        new()
        {
            FamilyId = familyId,
            UserId = _userId,
            Role = FamilyRole.Owner,
            User = new User
            {
                Email = "owner@example.com",
                DisplayName = "Family Owner"
            }
        }
    };

        _membershipRepository
            .Setup(x => x.IsSharedFamilyMemberAsync(
                familyId,
                _userId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _membershipRepository
            .Setup(x => x.GetMembersAsync(
                familyId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(memberships);

        var service = CreateService();

        var result = await service.GetMembersAsync(familyId);

        var member = Assert.Single(result);
        Assert.Equal(_userId, member.UserId);
        Assert.Equal("Family Owner", member.DisplayName);
        Assert.Equal("owner@example.com", member.Email);
        Assert.Equal("Owner", member.Role);

        _membershipRepository.Verify(
            x => x.GetMembersAsync(
                familyId,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetMembersAsync_WhenUserIsNotMember_ThrowsUnauthorizedAccessException()
    {
        var familyId = Guid.NewGuid();

        _membershipRepository
            .Setup(x => x.IsSharedFamilyMemberAsync(
                familyId,
                _userId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var service = CreateService();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.GetMembersAsync(familyId));

        _membershipRepository.Verify(
            x => x.GetMembersAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetMembersAsync_WhenUserIsUnauthenticated_ThrowsUnauthorizedAccessException()
    {
        var service = CreateService();

        _currentUser.SetupGet(x => x.UserId).Returns(Guid.Empty);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.GetMembersAsync(Guid.NewGuid()));

        _membershipRepository.Verify(
            x => x.IsSharedFamilyMemberAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
