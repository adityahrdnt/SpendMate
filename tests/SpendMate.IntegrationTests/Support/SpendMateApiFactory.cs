using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SpendMate.Domain.Entities;
using SpendMate.Domain.Enums;
using SpendMate.Infrastructure.Data;
using SpendMate.IntegrationTests.Support.Authentication;
using System.Net.Http.Json;
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

    public static readonly Guid SecondTestUserId =
        Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");

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
            services
                .AddAuthentication(options =>
                {
                    options.DefaultScheme = "TestOrCookie";
                    options.DefaultAuthenticateScheme = "TestOrCookie";
                    options.DefaultChallengeScheme = "TestOrCookie";
                    options.DefaultForbidScheme = "TestOrCookie";
                })
                .AddPolicyScheme(
                    "TestOrCookie",
                    "Test or Cookie Authentication",
                    options =>
                    {
                        options.ForwardDefaultSelector = context =>
                            context.Request.Headers.ContainsKey(
                                TestAuthenticationHandler.UserIdHeader)
                                ? TestAuthenticationHandler.SchemeName
                                : CookieAuthenticationDefaults.AuthenticationScheme;
                    })
                .AddScheme<
                    AuthenticationSchemeOptions,
                    TestAuthenticationHandler>(
                    TestAuthenticationHandler.SchemeName,
                    _ => { });
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
            Environment.SetEnvironmentVariable(
                connectionStringVariable,
                _postgres.GetConnectionString());

            using var client = CreateClient();
        }
        finally
        {
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

        dbContext.Users.Add(new User
        {
            Id = SecondTestUserId,
            Email = "integration-test-user-b@spendmate.local",
            DisplayName = "Integration Test User B"
        });

        dbContext.Families.AddRange(
            new Family
            {
                Id = TestFamilyId,
                Name = "Integration Test Family",
                Type = FamilyType.Personal,
                PersonalOwnerUserId = TestUserId
            },
            new Family
            {
                Id = NonMemberFamilyId,
                Name = "Non-member Test Family",
                Type = FamilyType.Shared,
                PersonalOwnerUserId = null
            },
            new Family
            {
                Id = CrossFamilyId,
                Name = "Cross-family Test Family",
                Type = FamilyType.Personal,
                PersonalOwnerUserId = SecondTestUserId
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
            UserId = SecondTestUserId,
            Role = FamilyRole.Owner
        });

        dbContext.FamilyMemberships.Add(new FamilyMembership
        {
            FamilyId = NonMemberFamilyId,
            UserId = SecondTestUserId,
            Role = FamilyRole.Owner
        });

        await dbContext.SaveChangesAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    public HttpClient CreateAuthenticatedClient(Guid? userId = null)
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

        client.DefaultRequestHeaders.Add(
            TestAuthenticationHandler.UserIdHeader,
            (userId ?? TestUserId).ToString());

        return client;
    }

    public async Task<HttpClient> CreateAuthenticatedClientWithCsrfAsync(
    Guid? userId = null)
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

        client.DefaultRequestHeaders.Add(
            TestAuthenticationHandler.UserIdHeader,
            (userId ?? TestUserId).ToString());

        var csrfResponse = await client.GetAsync("/api/auth/csrf");
        csrfResponse.EnsureSuccessStatusCode();

        var csrfToken = await csrfResponse.Content
            .ReadFromJsonAsync<CsrfTokenResponse>();

        if (string.IsNullOrWhiteSpace(csrfToken?.RequestToken))
        {
            client.Dispose();

            throw new InvalidOperationException(
                "The CSRF endpoint did not return a valid request token.");
        }

        client.DefaultRequestHeaders.Add(
            "X-CSRF-TOKEN",
            csrfToken.RequestToken);

        return client;
    }

    private sealed record CsrfTokenResponse(string RequestToken);
}
