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
    private readonly Mock<ICurrentUser> _currentUser = new();

    private readonly Guid _userId = Guid.NewGuid();

    private FamilyService CreateService()
    {
        _currentUser.SetupGet(x => x.UserId).Returns(_userId);

        return new FamilyService(
            _familyRepository.Object,
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
}
