using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SpendMate.Application.Abstractions;
using SpendMate.Infrastructure.Email;
using SpendMate.Infrastructure.Repositories;
using SpendMate.Infrastructure.Security;

namespace SpendMate.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddScoped<IExpenseRepository, ExpenseRepository>();
        services.AddScoped<IFamilyRepository, FamilyRepository>();
        services.AddScoped<
            IFamilyInvitationRepository,
            FamilyInvitationRepository>();
        services.AddScoped<
            IFamilyMembershipRepository,
            FamilyMembershipRepository>();

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IPasswordHasher, AspNetPasswordHasher>();
        services.AddScoped<IEmailSender, DevelopmentEmailSender>();

        services.Configure<InvitationLinkOptions>(
            configuration.GetSection(InvitationLinkOptions.SectionName));

        services.AddScoped<IInvitationLinkBuilder, InvitationLinkBuilder>();

        return services;
    }
}
