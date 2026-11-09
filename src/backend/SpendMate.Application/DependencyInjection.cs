using Microsoft.Extensions.DependencyInjection;
using SpendMate.Application.Auth.Services;
using SpendMate.Application.Expenses.Services;
using SpendMate.Application.Families.Services;

namespace SpendMate.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services)
    {
        services.AddScoped<ExpenseService>();
        services.AddScoped<AuthService>();
        services.AddScoped<EmailVerificationService>();
        services.AddScoped<FamilyService>();
        services.AddScoped<FamilyInvitationService>();

        return services;
    }
}
