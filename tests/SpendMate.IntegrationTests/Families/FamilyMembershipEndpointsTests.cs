using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpendMate.Application.Families.Dtos;
using SpendMate.Domain.Entities;
using SpendMate.Domain.Enums;
using SpendMate.Infrastructure.Data;
using SpendMate.IntegrationTests.Support;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace SpendMate.IntegrationTests.Families;

public sealed class FamilyMembershipEndpointsTests(
    SpendMateApiFactory factory)
    : IClassFixture<SpendMateApiFactory>, IAsyncLifetime
{
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _client = await factory.CreateAuthenticatedClientWithCsrfAsync();
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    private async Task<Guid> CreateSharedFamilyAsync(
        params (Guid UserId, FamilyRole Role)[] members)
    {
        var familyId = Guid.NewGuid();

        await using var scope = factory.Services.CreateAsyncScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<SpendMateDbContext>();

        var family = new Family
        {
            Id = familyId,
            Name = $"Membership Test Family {familyId:N}",
            Type = FamilyType.Shared,
            PersonalOwnerUserId = null
        };

        dbContext.Families.Add(family);

        foreach (var member in members)
        {
            dbContext.FamilyMemberships.Add(new FamilyMembership
            {
                FamilyId = familyId,
                UserId = member.UserId,
                Role = member.Role
            });
        }

        await dbContext.SaveChangesAsync();

        return familyId;
    }

    [Fact]
    public async Task GetMembers_AsOwner_ReturnsFamilyMembers()
    {
        // Arrange
        var familyId = await CreateSharedFamilyAsync(
            (SpendMateApiFactory.TestUserId, FamilyRole.Owner),
            (SpendMateApiFactory.SecondTestUserId, FamilyRole.Member));

        // Act
        var response = await _client.GetAsync(
            $"/api/families/{familyId}/members");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var members = await response.Content
            .ReadFromJsonAsync<List<FamilyMemberResponse>>();

        Assert.NotNull(members);
        Assert.Equal(2, members.Count);

        var owner = Assert.Single(
            members,
            member => member.UserId == SpendMateApiFactory.TestUserId);

        Assert.Equal("Owner", owner.Role);
        Assert.Equal("Integration Test User", owner.DisplayName);

        var member = Assert.Single(
            members,
            item => item.UserId == SpendMateApiFactory.SecondTestUserId);

        Assert.Equal("Member", member.Role);
        Assert.Equal("Integration Test User B", member.DisplayName);
    }

    [Fact]
    public async Task GetMembers_AsMember_ReturnsFamilyMembers()
    {
        // Arrange
        var familyId = await CreateSharedFamilyAsync(
            (SpendMateApiFactory.TestUserId, FamilyRole.Owner),
            (SpendMateApiFactory.SecondTestUserId, FamilyRole.Member));

        using var memberClient =
            await factory.CreateAuthenticatedClientWithCsrfAsync(
                SpendMateApiFactory.SecondTestUserId);

        // Act
        var response = await memberClient.GetAsync(
            $"/api/families/{familyId}/members");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var members = await response.Content
            .ReadFromJsonAsync<List<FamilyMemberResponse>>();

        Assert.NotNull(members);
        Assert.Equal(2, members.Count);
    }

    [Fact]
    public async Task GetMembers_AsNonMember_ReturnsUnauthorized()
    {
        // Arrange
        var familyId = await CreateSharedFamilyAsync(
            (SpendMateApiFactory.SecondTestUserId, FamilyRole.Owner));

        // Current client is TestUserId, which is not a member.

        // Act
        var response = await _client.GetAsync(
            $"/api/families/{familyId}/members");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMembers_AsPersonalFamilyMember_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/families/{SpendMateApiFactory.TestFamilyId}/members");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMembers_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync(
            $"/api/families/{SpendMateApiFactory.TestSharedFamilyId}/members");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdateMemberRole_AsOwner_PromotesMemberToOwner()
    {
        // Arrange
        var familyId = await CreateSharedFamilyAsync(
            (SpendMateApiFactory.TestUserId, FamilyRole.Owner),
            (SpendMateApiFactory.SecondTestUserId, FamilyRole.Member));

        var request = new
        {
            Role = "Owner"
        };

        // Act
        var response = await _client.PatchAsJsonAsync(
            $"/api/families/{familyId}/members/{SpendMateApiFactory.SecondTestUserId}/role",
            request);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<SpendMateDbContext>();

        var membership = await dbContext.FamilyMemberships
            .SingleAsync(m =>
                m.FamilyId == familyId &&
                m.UserId == SpendMateApiFactory.SecondTestUserId);

        Assert.Equal(FamilyRole.Owner, membership.Role);
    }

    [Fact]
    public async Task UpdateMemberRole_AsOwner_DemotesAnotherOwnerToMember()
    {
        // Arrange
        var thirdUserId = Guid.NewGuid();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider
                .GetRequiredService<SpendMateDbContext>();

            dbContext.Users.Add(new User
            {
                Id = thirdUserId,
                Email = $"{thirdUserId}@example.com",
                DisplayName = "Third Test User"
            });

            await dbContext.SaveChangesAsync();
        }

        var familyId = await CreateSharedFamilyAsync(
            (SpendMateApiFactory.TestUserId, FamilyRole.Owner),
            (SpendMateApiFactory.SecondTestUserId, FamilyRole.Owner),
            (thirdUserId, FamilyRole.Owner));

        // Act
        var response = await _client.PatchAsJsonAsync(
            $"/api/families/{familyId}/members/{SpendMateApiFactory.SecondTestUserId}/role",
            new { Role = "Member" });

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        await using var verificationScope =
            factory.Services.CreateAsyncScope();

        var verificationDb = verificationScope.ServiceProvider
            .GetRequiredService<SpendMateDbContext>();

        var membership = await verificationDb.FamilyMemberships
            .SingleAsync(m =>
                m.FamilyId == familyId &&
                m.UserId == SpendMateApiFactory.SecondTestUserId);

        Assert.Equal(FamilyRole.Member, membership.Role);

        var ownerCount = await verificationDb.FamilyMemberships
            .CountAsync(m =>
                m.FamilyId == familyId &&
                m.Role == FamilyRole.Owner);

        Assert.Equal(2, ownerCount);
    }

    [Fact]
    public async Task UpdateMemberRole_WhenTargetIsLastOwner_ReturnsConflict()
    {
        // Arrange
        var familyId = await CreateSharedFamilyAsync(
            (SpendMateApiFactory.TestUserId, FamilyRole.Owner),
            (SpendMateApiFactory.SecondTestUserId, FamilyRole.Member));

        // Act
        var response = await _client.PatchAsJsonAsync(
            $"/api/families/{familyId}/members/{SpendMateApiFactory.TestUserId}/role",
            new { Role = "Member" });

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<SpendMateDbContext>();

        var ownerCount = await dbContext.FamilyMemberships
            .CountAsync(m =>
                m.FamilyId == familyId &&
                m.Role == FamilyRole.Owner);

        Assert.Equal(1, ownerCount);
    }

    [Fact]
    public async Task UpdateMemberRole_AsMember_ReturnsUnauthorized()
    {
        // Arrange
        var familyId = await CreateSharedFamilyAsync(
            (SpendMateApiFactory.TestUserId, FamilyRole.Owner),
            (SpendMateApiFactory.SecondTestUserId, FamilyRole.Member));

        using var memberClient =
            await factory.CreateAuthenticatedClientWithCsrfAsync(
                SpendMateApiFactory.SecondTestUserId);

        // Act
        var response = await memberClient.PatchAsJsonAsync(
            $"/api/families/{familyId}/members/{SpendMateApiFactory.TestUserId}/role",
            new { Role = "Member" });

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<SpendMateDbContext>();

        var ownerMembership = await dbContext.FamilyMemberships
            .SingleAsync(m =>
                m.FamilyId == familyId &&
                m.UserId == SpendMateApiFactory.TestUserId);

        Assert.Equal(FamilyRole.Owner, ownerMembership.Role);
    }

    [Fact]
    public async Task UpdateMemberRole_AsOwnerChangingOwnRole_ReturnsConflict()
    {
        // Arrange
        var familyId = await CreateSharedFamilyAsync(
            (SpendMateApiFactory.TestUserId, FamilyRole.Owner),
            (SpendMateApiFactory.SecondTestUserId, FamilyRole.Member));

        // Act
        var response = await _client.PatchAsJsonAsync(
            $"/api/families/{familyId}/members/{SpendMateApiFactory.TestUserId}/role",
            new { Role = "Member" });

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<SpendMateDbContext>();

        var membership = await dbContext.FamilyMemberships
            .SingleAsync(m =>
                m.FamilyId == familyId &&
                m.UserId == SpendMateApiFactory.TestUserId);

        Assert.Equal(FamilyRole.Owner, membership.Role);
    }

    [Fact]
    public async Task UpdateMemberRole_WhenRoleIsAlreadyTheSame_ReturnsNoContent()
    {
        // Arrange
        var familyId = await CreateSharedFamilyAsync(
            (SpendMateApiFactory.TestUserId, FamilyRole.Owner),
            (SpendMateApiFactory.SecondTestUserId, FamilyRole.Member));

        // Act
        var response = await _client.PatchAsJsonAsync(
            $"/api/families/{familyId}/members/{SpendMateApiFactory.SecondTestUserId}/role",
            new { Role = "Member" });

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<SpendMateDbContext>();

        var membership = await dbContext.FamilyMemberships
            .SingleAsync(m =>
                m.FamilyId == familyId &&
                m.UserId == SpendMateApiFactory.SecondTestUserId);

        Assert.Equal(FamilyRole.Member, membership.Role);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Admin")]
    [InlineData("0")]
    [InlineData("3")]
    public async Task UpdateMemberRole_WithInvalidRole_ReturnsBadRequest(
    string role)
    {
        // Arrange
        var familyId = await CreateSharedFamilyAsync(
            (SpendMateApiFactory.TestUserId, FamilyRole.Owner),
            (SpendMateApiFactory.SecondTestUserId, FamilyRole.Member));

        // Act
        var response = await _client.PatchAsJsonAsync(
            $"/api/families/{familyId}/members/{SpendMateApiFactory.SecondTestUserId}/role",
            new { Role = role });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<SpendMateDbContext>();

        var membership = await dbContext.FamilyMemberships
            .SingleAsync(m =>
                m.FamilyId == familyId &&
                m.UserId == SpendMateApiFactory.SecondTestUserId);

        Assert.Equal(FamilyRole.Member, membership.Role);
    }

    [Fact]
    public async Task UpdateMemberRole_WhenTargetMembershipDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        var familyId = await CreateSharedFamilyAsync(
            (SpendMateApiFactory.TestUserId, FamilyRole.Owner));

        var nonMemberUserId = Guid.NewGuid();

        // Act
        var response = await _client.PatchAsJsonAsync(
            $"/api/families/{familyId}/members/{nonMemberUserId}/role",
            new { Role = "Owner" });

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RemoveMember_AsOwner_RemovesMember()
    {
        // Arrange
        var familyId = await CreateSharedFamilyAsync(
            (SpendMateApiFactory.TestUserId, FamilyRole.Owner),
            (SpendMateApiFactory.SecondTestUserId, FamilyRole.Member));

        // Act
        var response = await _client.DeleteAsync(
            $"/api/families/{familyId}/members/{SpendMateApiFactory.SecondTestUserId}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<SpendMateDbContext>();

        var membershipExists = await dbContext.FamilyMemberships
            .AnyAsync(m =>
                m.FamilyId == familyId &&
                m.UserId == SpendMateApiFactory.SecondTestUserId);

        Assert.False(membershipExists);

        var ownerStillExists = await dbContext.FamilyMemberships
            .AnyAsync(m =>
                m.FamilyId == familyId &&
                m.UserId == SpendMateApiFactory.TestUserId &&
                m.Role == FamilyRole.Owner);

        Assert.True(ownerStillExists);
    }

    [Fact]
    public async Task RemoveMember_WhenTargetIsLastOwner_ReturnsConflict()
    {
        // Arrange
        var familyId = await CreateSharedFamilyAsync(
            (SpendMateApiFactory.TestUserId, FamilyRole.Owner),
            (SpendMateApiFactory.SecondTestUserId, FamilyRole.Member));

        // Act
        var response = await _client.DeleteAsync(
            $"/api/families/{familyId}/members/{SpendMateApiFactory.TestUserId}");

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<SpendMateDbContext>();

        var ownerMembership = await dbContext.FamilyMemberships
            .SingleOrDefaultAsync(m =>
                m.FamilyId == familyId &&
                m.UserId == SpendMateApiFactory.TestUserId);

        Assert.NotNull(ownerMembership);
        Assert.Equal(FamilyRole.Owner, ownerMembership.Role);

        var ownerCount = await dbContext.FamilyMemberships
            .CountAsync(m =>
                m.FamilyId == familyId &&
                m.Role == FamilyRole.Owner);

        Assert.Equal(1, ownerCount);
    }

    [Fact]
    public async Task RemoveMember_AsOwner_RemovesAnotherOwnerWhenOtherOwnerRemains()
    {
        // Arrange
        var thirdUserId = Guid.NewGuid();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider
                .GetRequiredService<SpendMateDbContext>();

            dbContext.Users.Add(new User
            {
                Id = thirdUserId,
                Email = $"{thirdUserId}@example.com",
                DisplayName = "Third Test User"
            });

            await dbContext.SaveChangesAsync();
        }

        var familyId = await CreateSharedFamilyAsync(
            (SpendMateApiFactory.TestUserId, FamilyRole.Owner),
            (SpendMateApiFactory.SecondTestUserId, FamilyRole.Owner),
            (thirdUserId, FamilyRole.Owner));

        // Act
        var response = await _client.DeleteAsync(
            $"/api/families/{familyId}/members/{SpendMateApiFactory.SecondTestUserId}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        await using var verificationScope =
            factory.Services.CreateAsyncScope();

        var verificationDb = verificationScope.ServiceProvider
            .GetRequiredService<SpendMateDbContext>();

        var removedMembershipExists = await verificationDb.FamilyMemberships
            .AnyAsync(m =>
                m.FamilyId == familyId &&
                m.UserId == SpendMateApiFactory.SecondTestUserId);

        Assert.False(removedMembershipExists);

        var ownerCount = await verificationDb.FamilyMemberships
            .CountAsync(m =>
                m.FamilyId == familyId &&
                m.Role == FamilyRole.Owner);

        Assert.Equal(2, ownerCount);
    }

    [Fact]
    public async Task RemoveMember_AsMember_ReturnsUnauthorized()
    {
        // Arrange
        var familyId = await CreateSharedFamilyAsync(
            (SpendMateApiFactory.TestUserId, FamilyRole.Owner),
            (SpendMateApiFactory.SecondTestUserId, FamilyRole.Member));

        using var memberClient =
            await factory.CreateAuthenticatedClientWithCsrfAsync(
                SpendMateApiFactory.SecondTestUserId);

        // Act
        var response = await memberClient.DeleteAsync(
            $"/api/families/{familyId}/members/{SpendMateApiFactory.TestUserId}");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<SpendMateDbContext>();

        var ownerExists = await dbContext.FamilyMemberships
            .AnyAsync(m =>
                m.FamilyId == familyId &&
                m.UserId == SpendMateApiFactory.TestUserId &&
                m.Role == FamilyRole.Owner);

        Assert.True(ownerExists);
    }

    [Fact]
    public async Task RemoveMember_AsOwner_RemovesOwnMembershipWhenAnotherOwnerRemains()
    {
        // Arrange
        var familyId = await CreateSharedFamilyAsync(
            (SpendMateApiFactory.TestUserId, FamilyRole.Owner),
            (SpendMateApiFactory.SecondTestUserId, FamilyRole.Owner));

        // Act
        var response = await _client.DeleteAsync(
            $"/api/families/{familyId}/members/{SpendMateApiFactory.TestUserId}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<SpendMateDbContext>();

        var ownMembershipExists = await dbContext.FamilyMemberships
            .AnyAsync(m =>
                m.FamilyId == familyId &&
                m.UserId == SpendMateApiFactory.TestUserId);

        Assert.False(ownMembershipExists);

        var remainingOwner = await dbContext.FamilyMemberships
            .SingleAsync(m =>
                m.FamilyId == familyId &&
                m.Role == FamilyRole.Owner);

        Assert.Equal(SpendMateApiFactory.SecondTestUserId, remainingOwner.UserId);
    }

    [Fact]
    public async Task RemoveMember_WhenTargetMembershipDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        var familyId = await CreateSharedFamilyAsync(
            (SpendMateApiFactory.TestUserId, FamilyRole.Owner));

        var nonMemberUserId = Guid.NewGuid();

        // Act
        var response = await _client.DeleteAsync(
            $"/api/families/{familyId}/members/{nonMemberUserId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
