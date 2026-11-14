
using Moq;
using SpendMate.Application.Abstractions;
using SpendMate.Application.Auth.Dtos;
using SpendMate.Application.Auth.Services;
using SpendMate.Domain.Entities;
using SpendMate.Domain.Enums;

namespace SpendMate.UnitTests.Auth;

public class AuthServiceTests
{
    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IPasswordHasher> _passwordHasher = new();

    private AuthService CreateService()
    {
        return new AuthService(
            _userRepository.Object,
            _passwordHasher.Object);
    }

    [Fact]
    public async Task RegisterAsync_WhenRequestIsValid_CreatesUserAndFamily()
    {
        _userRepository
            .Setup(x => x.GetByEmailAsync(
                "aditya@example.com",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        _passwordHasher
            .Setup(x => x.HashPassword(
                It.IsAny<User>(),
                "Password123"))
            .Returns("hashed-password");

        var service = CreateService();

        var result = await service.RegisterAsync(
            new RegisterRequest(
                " Aditya@Example.com ",
                "Aditya",
                "Password123",
                "Herdianto Family"));

        Assert.Equal("aditya@example.com", result.Email);
        Assert.Equal("Aditya", result.DisplayName);
        Assert.Equal("Herdianto Family", result.FamilyName);
        Assert.NotEqual(Guid.Empty, result.UserId);
        Assert.NotEqual(Guid.Empty, result.FamilyId);

        _userRepository.Verify(
            x => x.AddWithActiveFamilyAsync(
                It.Is<User>(user =>
                    user.Email == "aditya@example.com" &&
                    user.DisplayName == "Aditya" &&
                    user.PasswordHash == "hashed-password" &&
                    user.Memberships.Count == 1 &&
                    user.Memberships.First().Role == FamilyRole.Owner &&
                    user.Memberships.First().Family.Type == FamilyType.Personal &&
                    user.Memberships.First().Family.PersonalOwnerUserId == user.Id &&
                    user.Memberships.First().Family.Name == "Herdianto Family"),
                It.Is<Guid>(familyId => familyId == result.FamilyId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RegisterAsync_WhenEmailAlreadyExists_ThrowsInvalidOperationException()
    {
        var existingUser = new User
        {
            Email = "aditya@example.com",
            DisplayName = "Existing User"
        };

        _userRepository
            .Setup(x => x.GetByEmailAsync(
                "aditya@example.com",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingUser);

        var service = CreateService();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RegisterAsync(
                new RegisterRequest(
                    "aditya@example.com",
                    "Aditya",
                    "Password123",
                    "Herdianto Family")));

        _userRepository.Verify(
            x => x.AddWithActiveFamilyAsync(
                It.IsAny<User>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RegisterAsync_WhenFamilyNameIsEmpty_ThrowsArgumentException()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.RegisterAsync(
                new RegisterRequest(
                    "aditya@example.com",
                    "Aditya",
                    "Password123",
                    "  ")));

        _userRepository.Verify(
            x => x.AddWithActiveFamilyAsync(
                It.IsAny<User>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RegisterAsync_WhenPasswordIsTooShort_ThrowsArgumentException()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.RegisterAsync(
                new RegisterRequest(
                    "aditya@example.com",
                    "Aditya",
                    "1234567",
                    "Herdianto Family")));

        _userRepository.Verify(
            x => x.AddWithActiveFamilyAsync(
                It.IsAny<User>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RegisterAsync_WhenEmailIsInvalid_ThrowsArgumentException()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.RegisterAsync(
                new RegisterRequest(
                    "invalid-email",
                    "Aditya",
                    "Password123",
                    "Herdianto Family")));

        _userRepository.Verify(
            x => x.AddWithActiveFamilyAsync(
                It.IsAny<User>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task LoginAsync_WhenCredentialsAreValid_ReturnsPersonalFamily()
    {
        var user = new User
        {
            Email = "aditya@example.com",
            DisplayName = "Aditya",
            PasswordHash = "hashed-password"
        };

        var sharedFamily = new Family
        {
            Name = "Shared Family",
            Type = FamilyType.Shared
        };

        var personalFamily = new Family
        {
            Name = "Herdianto Family",
            Type = FamilyType.Personal,
            PersonalOwnerUserId = user.Id
        };

        var sharedMembership = new FamilyMembership
        {
            UserId = user.Id,
            FamilyId = sharedFamily.Id,
            User = user,
            Family = sharedFamily,
            Role = FamilyRole.Member,
            JoinedAtUtc = DateTimeOffset.UtcNow.AddDays(-10)
        };

        var personalMembership = new FamilyMembership
        {
            UserId = user.Id,
            FamilyId = personalFamily.Id,
            User = user,
            Family = personalFamily,
            Role = FamilyRole.Owner,
            JoinedAtUtc = DateTimeOffset.UtcNow
        };

        user.Memberships.Add(sharedMembership);
        user.Memberships.Add(personalMembership);

        _userRepository
            .Setup(x => x.GetByEmailAsync(
                "aditya@example.com",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _passwordHasher
            .Setup(x => x.VerifyPassword(
                user,
                "hashed-password",
                "Password123"))
            .Returns(true);

        user.ActiveFamilyId = Guid.NewGuid();

        var service = CreateService();

        var result = await service.LoginAsync(
            new LoginRequest(
                "ADITYA@example.com",
                "Password123"));

        Assert.Equal(user.Id, result.UserId);
        Assert.Equal("aditya@example.com", result.Email);
        Assert.Equal("Aditya", result.DisplayName);
        Assert.Equal(personalFamily.Id, result.FamilyId);
        Assert.Equal("Herdianto Family", result.FamilyName);
        Assert.Equal(personalFamily.Id, user.ActiveFamilyId);

        _userRepository.Verify(
            x => x.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task LoginAsync_WhenPasswordIsIncorrect_ThrowsUnauthorizedAccessException()
    {
        var user = new User
        {
            Email = "aditya@example.com",
            DisplayName = "Aditya",
            PasswordHash = "hashed-password"
        };

        _userRepository
            .Setup(x => x.GetByEmailAsync(
                "aditya@example.com",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _passwordHasher
            .Setup(x => x.VerifyPassword(
                user,
                "hashed-password",
                "WrongPassword"))
            .Returns(false);

        var service = CreateService();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.LoginAsync(
                new LoginRequest(
                    "aditya@example.com",
                    "WrongPassword")));
    }

    [Fact]
    public async Task LoginAsync_WhenUserHasNoPasswordHash_ThrowsUnauthorizedAccessException()
    {
        var user = new User
        {
            Email = "legacy@example.com",
            DisplayName = "Legacy User",
            PasswordHash = null
        };

        _userRepository
            .Setup(x => x.GetByEmailAsync(
                "legacy@example.com",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var service = CreateService();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.LoginAsync(
                new LoginRequest(
                    "legacy@example.com",
                    "Password123")));

        _passwordHasher.Verify(
            x => x.VerifyPassword(
                It.IsAny<User>(),
                It.IsAny<string>(),
                It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task LoginAsync_WhenUserHasNoFamilyMembership_ThrowsUnauthorizedAccessException()
    {
        var user = new User
        {
            Email = "aditya@example.com",
            DisplayName = "Aditya",
            PasswordHash = "hashed-password"
        };

        _userRepository
            .Setup(x => x.GetByEmailAsync(
                "aditya@example.com",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _passwordHasher
            .Setup(x => x.VerifyPassword(
                user,
                "hashed-password",
                "Password123"))
            .Returns(true);

        var service = CreateService();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.LoginAsync(
                new LoginRequest(
                    "aditya@example.com",
                    "Password123")));
    }

    [Fact]
    public async Task LoginAsync_WhenUserHasOnlySharedFamily_ThrowsUnauthorizedAccessException()
    {
        var user = new User
        {
            Email = "aditya@example.com",
            DisplayName = "Aditya",
            PasswordHash = "hashed-password"
        };

        var family = new Family
        {
            Name = "Shared Family",
            Type = FamilyType.Shared
        };

        var membership = new FamilyMembership
        {
            UserId = user.Id,
            FamilyId = family.Id,
            User = user,
            Family = family,
            Role = FamilyRole.Member
        };

        user.Memberships.Add(membership);

        _userRepository
            .Setup(x => x.GetByEmailAsync(
                "aditya@example.com",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _passwordHasher
            .Setup(x => x.VerifyPassword(
                user,
                "hashed-password",
                "Password123"))
            .Returns(true);

        var service = CreateService();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.LoginAsync(
                new LoginRequest(
                    "aditya@example.com",
                    "Password123")));
    }

    [Fact]
    public async Task GetCurrentUserAsync_WhenActiveFamilyIsValid_ReturnsActiveFamily()
    {
        var user = new User
        {
            Email = "aditya@example.com",
            DisplayName = "Aditya"
        };

        var personalFamily = new Family
        {
            Name = "Personal Family",
            Type = FamilyType.Personal,
            PersonalOwnerUserId = user.Id
        };

        var sharedFamily = new Family
        {
            Name = "Shared Family",
            Type = FamilyType.Shared
        };

        user.Memberships.Add(new FamilyMembership
        {
            UserId = user.Id,
            FamilyId = personalFamily.Id,
            User = user,
            Family = personalFamily,
            Role = FamilyRole.Owner
        });

        user.Memberships.Add(new FamilyMembership
        {
            UserId = user.Id,
            FamilyId = sharedFamily.Id,
            User = user,
            Family = sharedFamily,
            Role = FamilyRole.Member
        });

        user.ActiveFamilyId = sharedFamily.Id;

        _userRepository
            .Setup(x => x.GetByIdWithMembershipsAsync(
                user.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var service = CreateService();

        var result = await service.GetCurrentUserAsync(user.Id);

        Assert.Equal(user.Id, result.UserId);
        Assert.Equal(sharedFamily.Id, result.FamilyId);
        Assert.Equal("Shared Family", result.FamilyName);

        _userRepository.Verify(
            x => x.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetCurrentUserAsync_WhenActiveFamilyIsInvalid_FallsBackToPersonalFamily()
    {
        var user = new User
        {
            Email = "aditya@example.com",
            DisplayName = "Aditya",
            ActiveFamilyId = Guid.NewGuid()
        };

        var personalFamily = new Family
        {
            Name = "Personal Family",
            Type = FamilyType.Personal,
            PersonalOwnerUserId = user.Id
        };

        user.Memberships.Add(new FamilyMembership
        {
            UserId = user.Id,
            FamilyId = personalFamily.Id,
            User = user,
            Family = personalFamily,
            Role = FamilyRole.Owner
        });

        _userRepository
            .Setup(x => x.GetByIdWithMembershipsAsync(
                user.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var service = CreateService();

        var result = await service.GetCurrentUserAsync(user.Id);

        Assert.Equal(personalFamily.Id, result.FamilyId);
        Assert.Equal(personalFamily.Id, user.ActiveFamilyId);

        _userRepository.Verify(
            x => x.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetCurrentUserAsync_WhenUserDoesNotExist_ThrowsUnauthorizedAccessException()
    {
        var userId = Guid.NewGuid();

        _userRepository
            .Setup(x => x.GetByIdWithMembershipsAsync(
                userId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var service = CreateService();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.GetCurrentUserAsync(userId));

        _userRepository.Verify(
            x => x.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
