using Microsoft.Extensions.DependencyInjection;
using SpendMate.Application.Expenses.Services;

namespace SpendMate.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services)
    {
        services.AddScoped<ExpenseService>();

        return services;
    }
}
