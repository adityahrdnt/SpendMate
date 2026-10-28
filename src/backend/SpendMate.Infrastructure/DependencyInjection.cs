using Microsoft.Extensions.DependencyInjection;
using SpendMate.Application.Abstractions;
using SpendMate.Infrastructure.Repositories;

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

        return services;
    }
}
