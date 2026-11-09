using Moq;
using SpendMate.Application.Abstractions;
using SpendMate.Application.Families.Dtos;
using SpendMate.Application.Families.Services;
using SpendMate.Domain.Entities;
using SpendMate.Domain.Enums;
using System.Security.Cryptography;
using System.Text;

namespace SpendMate.UnitTests.Families;

public class FamilyInvitationServiceTests
{
    private readonly Mock<IFamilyRepository> _familyRepository = new();
    private readonly Mock<IFamilyMembershipRepository> _membershipRepository = new();
    private readonly Mock<IFamilyInvitationRepository> _invitationRepository = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Mock<IEmailSender> _emailSender = new();
    private readonly Mock<IInvitationLinkBuilder> _invitationLinkBuilder = new();

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _familyId = Guid.NewGuid();

    private FamilyInvitationService CreateService()
    {
        _currentUser.SetupGet(x => x.UserId).Returns(_userId);

        _familyRepository
            .Setup(x => x.GetSharedFamilyOwnedByUserAsync(
                _familyId,
                _userId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Family
            {
                Id = _familyId,
                Name = "Test Family",
                Type = FamilyType.Shared
            });

        _membershipRepository
            .Setup(x => x.IsMemberByEmailAsync(
                _familyId,
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _invitationRepository
            .Setup(x => x.GetPendingByFamilyAndEmailAsync(
                _familyId,
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((FamilyInvitation?)null);

        _invitationLinkBuilder
            .Setup(x => x.Build(It.IsAny<string>()))
            .Returns((string token) =>
                $"http://localhost:5173/invitations/accept?token={token}");

        return new FamilyInvitationService(
            _familyRepository.Object,
            _membershipRepository.Object,
            _invitationRepository.Object,
            _currentUser.Object,
            _emailSender.Object,
            _invitationLinkBuilder.Object);
    }

    [Fact]
    public async Task CreateAsync_WhenValidRequest_CreatesInvitation()
    {
        var service = CreateService();

        FamilyInvitation? savedInvitation = null;

        _invitationRepository
            .Setup(x => x.AddAsync(
                It.IsAny<FamilyInvitation>(),
                It.IsAny<CancellationToken>()))
            .Callback<FamilyInvitation, CancellationToken>(
                (invitation, _) => savedInvitation = invitation)
            .Returns(Task.CompletedTask);

        var result = await service.CreateAsync(
            _familyId,
            new CreateFamilyInvitationRequest(" Guest@Example.com "));

        Assert.NotNull(savedInvitation);
        Assert.Equal("guest@example.com", savedInvitation.Email);
        Assert.Equal("guest@example.com", savedInvitation.NormalizedEmail);
        Assert.Equal(_familyId, savedInvitation.FamilyId);
        Assert.Equal(_userId, savedInvitation.InvitedByUserId);
        Assert.Equal(FamilyInvitationStatus.Pending, savedInvitation.Status);

        // Token must be URL-safe Base64 without padding.
        Assert.Equal(43, result.Token.Length);
        Assert.DoesNotContain('+', result.Token);
        Assert.DoesNotContain('/', result.Token);
        Assert.DoesNotContain('=', result.Token);

        Assert.All(
            result.Token,
            character => Assert.True(
                char.IsAsciiLetterOrDigit(character) ||
                character is '-' or '_'));

        // Token must contain 32 random bytes (256-bit entropy).
        var base64Token = result.Token
            .Replace('-', '+')
            .Replace('_', '/');

        var paddedToken = base64Token.PadRight(
            (base64Token.Length + 3) / 4 * 4,
            '=');

        var decodedToken = Convert.FromBase64String(paddedToken);

        Assert.Equal(32, decodedToken.Length);

        // Database must store SHA-256 hash, never the raw token.
        var expectedHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(result.Token)));

        Assert.Equal(64, savedInvitation.TokenHash.Length);
        Assert.Equal(expectedHash, savedInvitation.TokenHash);
        Assert.NotEqual(result.Token, savedInvitation.TokenHash);

        _invitationLinkBuilder.Verify(
            x => x.Build(result.Token),
            Times.Once);

        _emailSender.Verify(
            x => x.SendAsync(
                "guest@example.com",
                It.IsAny<string>(),
                It.Is<string>(body => body.Contains(
                    $"token={result.Token}")),
                It.IsAny<CancellationToken>()),
            Times.Once);

        Assert.False(result.WasResent);

        Assert.Equal(savedInvitation.Id, result.Invitation.Id);
        Assert.Equal("Pending", result.Invitation.Status);

        Assert.Equal(
            TimeSpan.FromDays(7),
            savedInvitation.ExpiresAtUtc - savedInvitation.CreatedAtUtc);

        _invitationRepository.Verify(
            x => x.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WhenPendingInvitationExists_RotatesToken()
    {
        var service = CreateService();

        var existingInvitation = new FamilyInvitation
        {
            FamilyId = _familyId,
            InvitedByUserId = _userId,
            Email = "guest@example.com",
            NormalizedEmail = "guest@example.com",
            TokenHash = "OLD_HASH",
            CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(2)
        };

        _invitationRepository
            .Setup(x => x.GetPendingByFamilyAndEmailAsync(
                _familyId,
                "guest@example.com",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingInvitation);

        var originalCreatedAt = existingInvitation.CreatedAtUtc;

        var result = await service.CreateAsync(
            _familyId,
            new CreateFamilyInvitationRequest("guest@example.com"));

        Assert.True(result.WasResent);
        Assert.Equal(originalCreatedAt, existingInvitation.CreatedAtUtc);
        Assert.NotEqual("OLD_HASH", existingInvitation.TokenHash);
        Assert.Equal(64, existingInvitation.TokenHash.Length);

        Assert.InRange(
            existingInvitation.ExpiresAtUtc,
            DateTimeOffset.UtcNow.AddDays(7).AddSeconds(-2),
            DateTimeOffset.UtcNow.AddDays(7).AddSeconds(2));

        _invitationRepository.Verify(
            x => x.AddAsync(
                It.IsAny<FamilyInvitation>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        _invitationRepository.Verify(
            x => x.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WhenPendingInvitationExpired_MarksOldAndCreatesNew()
    {
        var service = CreateService();

        var expiredInvitation = new FamilyInvitation
        {
            FamilyId = _familyId,
            InvitedByUserId = _userId,
            Email = "guest@example.com",
            NormalizedEmail = "guest@example.com",
            TokenHash = "OLD_HASH",
            CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-10),
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(-3)
        };

        _invitationRepository
            .Setup(x => x.GetPendingByFamilyAndEmailAsync(
                _familyId,
                "guest@example.com",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expiredInvitation);

        FamilyInvitation? newInvitation = null;

        _invitationRepository
            .Setup(x => x.AddAsync(
                It.IsAny<FamilyInvitation>(),
                It.IsAny<CancellationToken>()))
            .Callback<FamilyInvitation, CancellationToken>(
                (invitation, _) => newInvitation = invitation)
            .Returns(Task.CompletedTask);

        var result = await service.CreateAsync(
            _familyId,
            new CreateFamilyInvitationRequest("guest@example.com"));

        Assert.Equal(FamilyInvitationStatus.Expired, expiredInvitation.Status);

        Assert.NotNull(newInvitation);
        Assert.NotEqual(expiredInvitation.Id, newInvitation.Id);
        Assert.Equal(FamilyInvitationStatus.Pending, newInvitation.Status);
        Assert.False(result.WasResent);
    }

    [Fact]
    public async Task CreateAsync_WhenEmailAlreadyMember_ThrowsInvalidOperationException()
    {
        var service = CreateService();

        _membershipRepository
            .Setup(x => x.IsMemberByEmailAsync(
                _familyId,
                "guest@example.com",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(
                _familyId,
                new CreateFamilyInvitationRequest("guest@example.com")));

        _invitationRepository.Verify(
            x => x.AddAsync(
                It.IsAny<FamilyInvitation>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WhenFamilyNotOwnedByCurrentUser_ThrowsUnauthorized()
    {
        var service = CreateService();

        _familyRepository
            .Setup(x => x.GetSharedFamilyOwnedByUserAsync(
                _familyId,
                _userId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Family?)null);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.CreateAsync(
                _familyId,
                new CreateFamilyInvitationRequest("guest@example.com")));
    }

    [Fact]
    public async Task CreateAsync_WhenUserUnauthenticated_ThrowsUnauthorized()
    {
        var service = CreateService();

        _currentUser.SetupGet(x => x.UserId).Returns(Guid.Empty);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.CreateAsync(
                _familyId,
                new CreateFamilyInvitationRequest("guest@example.com")));

        _familyRepository.Verify(
            x => x.GetSharedFamilyOwnedByUserAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid-email")]
    [InlineData("invalid@")]
    public async Task CreateAsync_WhenEmailInvalid_ThrowsArgumentException(
        string email)
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAsync(
                _familyId,
                new CreateFamilyInvitationRequest(email)));

        _familyRepository.Verify(
            x => x.GetSharedFamilyOwnedByUserAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RevokeAsync_WhenPendingInvitationExists_RevokesInvitation()
    {
        var service = CreateService();

        var invitation = new FamilyInvitation
        {
            Id = Guid.NewGuid(),
            FamilyId = _familyId,
            InvitedByUserId = _userId,
            Email = "guest@example.com",
            NormalizedEmail = "guest@example.com",
            TokenHash = "TEST_HASH",
            Status = FamilyInvitationStatus.Pending,
            CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(6)
        };

        _invitationRepository
            .Setup(x => x.GetByIdAndFamilyAsync(
                invitation.Id,
                _familyId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);

        var result = await service.RevokeAsync(
            _familyId,
            invitation.Id);

        Assert.Equal(FamilyInvitationStatus.Revoked, invitation.Status);
        Assert.NotNull(invitation.RevokedAtUtc);
        Assert.Equal("Revoked", result.Status);

        _invitationRepository.Verify(
            x => x.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RevokeAsync_WhenInvitationExpired_MarksAsExpired()
    {
        var service = CreateService();

        var invitation = new FamilyInvitation
        {
            Id = Guid.NewGuid(),
            FamilyId = _familyId,
            InvitedByUserId = _userId,
            Email = "guest@example.com",
            NormalizedEmail = "guest@example.com",
            TokenHash = "TEST_HASH",
            Status = FamilyInvitationStatus.Pending,
            CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-10),
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(-3)
        };

        _invitationRepository
            .Setup(x => x.GetByIdAndFamilyAsync(
                invitation.Id,
                _familyId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);

        var result = await service.RevokeAsync(
            _familyId,
            invitation.Id);

        Assert.Equal(FamilyInvitationStatus.Expired, invitation.Status);
        Assert.Null(invitation.RevokedAtUtc);
        Assert.Equal("Expired", result.Status);

        _invitationRepository.Verify(
            x => x.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RevokeAsync_WhenInvitationNotFound_ThrowsKeyNotFoundException()
    {
        var service = CreateService();

        var invitationId = Guid.NewGuid();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.RevokeAsync(_familyId, invitationId));

        _invitationRepository.Verify(
            x => x.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData(FamilyInvitationStatus.Accepted)]
    [InlineData(FamilyInvitationStatus.Revoked)]
    [InlineData(FamilyInvitationStatus.Expired)]
    public async Task RevokeAsync_WhenInvitationIsNotPending_ThrowsInvalidOperationException(
    FamilyInvitationStatus status)
    {
        var service = CreateService();

        var invitation = new FamilyInvitation
        {
            Id = Guid.NewGuid(),
            FamilyId = _familyId,
            InvitedByUserId = _userId,
            Email = "guest@example.com",
            NormalizedEmail = "guest@example.com",
            TokenHash = "TEST_HASH",
            Status = status,
            CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(6)
        };

        _invitationRepository
            .Setup(x => x.GetByIdAndFamilyAsync(
                invitation.Id,
                _familyId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RevokeAsync(_familyId, invitation.Id));

        _invitationRepository.Verify(
            x => x.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
