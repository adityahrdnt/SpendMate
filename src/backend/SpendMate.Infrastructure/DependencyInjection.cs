using Microsoft.Extensions.DependencyInjection;
using SpendMate.Application.Abstractions;
using SpendMate.Infrastructure.Repositories;
using SpendMate.Infrastructure.Security;

namespace SpendMate.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services)
    {
        services.AddScoped<IExpenseRepository, ExpenseRepository>();
        services.AddScoped<
            IFamilyMembershipRepository,
            FamilyMembershipRepository>();
        services.AddScoped<IFamilyRepository, FamilyRepository>();

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IPasswordHasher, AspNetPasswordHasher>();

        return services;
    }
}
