using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SpendMate.Application.Abstractions;
using SpendMate.Domain.Entities;
using SpendMate.Domain.Enums;
using SpendMate.Infrastructure.Data;
using Testcontainers.PostgreSql;
using Xunit;

namespace SpendMate.IntegrationTests.Support;

public sealed class SpendMateApiFactory
    : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres =
        new PostgreSqlBuilder("postgres:17")
            .WithDatabase("spendmate_test")
            .WithUsername("spendmate")
            .WithPassword("SpendMateTest_2026!")
            .Build();

    public static readonly Guid TestUserId =
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    public static readonly Guid TestFamilyId =
        Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    public static readonly Guid NonMemberFamilyId =
        Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    public static readonly Guid CrossFamilyId =
        Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ICurrentUser>();
            services.AddScoped<ICurrentUser, TestCurrentUser>();
        });
    }

    async Task IAsyncLifetime.InitializeAsync()
    {
        await _postgres.StartAsync();

        const string connectionStringVariable =
            "ConnectionStrings__DefaultConnection";

        var originalConnectionString =
            Environment.GetEnvironmentVariable(connectionStringVariable);

        try
        {
            // Provide the connection string before Program.cs starts.
            Environment.SetEnvironmentVariable(
                connectionStringVariable,
                _postgres.GetConnectionString());

            // Starts the API host and applies database migrations.
            using var client = CreateClient();
        }
        finally
        {
            // Restore the original process environment.
            Environment.SetEnvironmentVariable(
                connectionStringVariable,
                originalConnectionString);
        }

        await using var scope = Services.CreateAsyncScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<SpendMateDbContext>();

        dbContext.Users.Add(new User
        {
            Id = TestUserId,
            Email = "integration-test@spendmate.local",
            DisplayName = "Integration Test User"
        });

        dbContext.Families.AddRange(
            new Family
            {
                Id = TestFamilyId,
                Name = "Integration Test Family"
            },
            new Family
            {
                Id = NonMemberFamilyId,
                Name = "Non-member Test Family"
            },
            new Family
            {
                Id = CrossFamilyId,
                Name = "Cross-family Test Family"
            });

        dbContext.FamilyMemberships.Add(new FamilyMembership
        {
            FamilyId = TestFamilyId,
            UserId = TestUserId,
            Role = FamilyRole.Owner
        });

        dbContext.FamilyMemberships.Add(new FamilyMembership
        {
            FamilyId = CrossFamilyId,
            UserId = TestUserId,
            Role = FamilyRole.Owner
        });

        await dbContext.SaveChangesAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}

public sealed class TestCurrentUser : ICurrentUser
{
    public Guid UserId => SpendMateApiFactory.TestUserId;
}
