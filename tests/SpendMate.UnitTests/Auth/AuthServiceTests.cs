
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
            x => x.AddAsync(
                It.Is<User>(user =>
                    user.Email == "aditya@example.com" &&
                    user.DisplayName == "Aditya" &&
                    user.PasswordHash == "hashed-password" &&
                    user.Memberships.Count == 1 &&
                    user.Memberships.First().Role == FamilyRole.Owner &&
                    user.Memberships.First().Family.Name == "Herdianto Family"),
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
            x => x.AddAsync(
                It.IsAny<User>(),
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
            x => x.AddAsync(
                It.IsAny<User>(),
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
            x => x.AddAsync(
                It.IsAny<User>(),
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
            x => x.AddAsync(
                It.IsAny<User>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task LoginAsync_WhenCredentialsAreValid_ReturnsUserAndFamily()
    {
        var family = new Family
        {
            Name = "Herdianto Family"
        };

        var user = new User
        {
            Email = "aditya@example.com",
            DisplayName = "Aditya",
            PasswordHash = "hashed-password"
        };

        var membership = new FamilyMembership
        {
            UserId = user.Id,
            FamilyId = family.Id,
            User = user,
            Family = family,
            Role = FamilyRole.Owner
        };

        user.Memberships.Add(membership);
        family.Memberships.Add(membership);

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

        var result = await service.LoginAsync(
            new LoginRequest(
                "ADITYA@example.com",
                "Password123"));

        Assert.Equal(user.Id, result.UserId);
        Assert.Equal("aditya@example.com", result.Email);
        Assert.Equal("Aditya", result.DisplayName);
        Assert.Equal(family.Id, result.FamilyId);
        Assert.Equal("Herdianto Family", result.FamilyName);
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
}
