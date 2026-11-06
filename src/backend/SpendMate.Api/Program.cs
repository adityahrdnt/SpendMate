using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SpendMate.Api.Development;
using SpendMate.Api.Endpoints;
using SpendMate.Api.Security;
using SpendMate.Application;
using SpendMate.Application.Abstractions;
using SpendMate.Infrastructure;
using SpendMate.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

// OpenAPI
builder.Services.AddOpenApi();

// Database
var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Connection string 'DefaultConnection' is not configured.");

builder.Services.AddDbContext<SpendMateDbContext>(options =>
    options.UseNpgsql(connectionString));

// Application
builder.Services.AddApplication();
builder.Services.AddInfrastructure();

// Authentication
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = ".SpendMate.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.Path = "/";

        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;

        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };

        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";

    options.Cookie.Name = ".SpendMate.Csrf";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.Path = "/";
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, ClaimsCurrentUser>();

// Health checks
builder.Services
    .AddHealthChecks()
    .AddDbContextCheck<SpendMateDbContext>(
        name: "postgres",
        tags: ["ready"]);

var app = builder.Build();

if (app.Environment.IsEnvironment("Testing"))
{
    await using var scope = app.Services.CreateAsyncScope();

    var dbContext = scope.ServiceProvider
        .GetRequiredService<SpendMateDbContext>();

    await dbContext.Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    await using var scope = app.Services.CreateAsyncScope();

    var dbContext = scope.ServiceProvider
        .GetRequiredService<SpendMateDbContext>();

    await DevelopmentDataSeeder.SeedAsync(dbContext);
}

// OpenAPI
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Health: API liveness
app.MapHealthChecks("/health", new()
{
    Predicate = _ => false
});

// Readiness: API and database
app.MapHealthChecks("/health/ready", new()
{
    Predicate = check => check.Tags.Contains("ready")
});

app.UseHttpsRedirection();

app.UseAuthentication();

// Validate CSRF tokens for all state-changing API requests.
app.Use(async (context, next) =>
{
    var isUnsafeMethod =
        HttpMethods.IsPost(context.Request.Method) ||
        HttpMethods.IsPut(context.Request.Method) ||
        HttpMethods.IsPatch(context.Request.Method) ||
        HttpMethods.IsDelete(context.Request.Method);

    if (isUnsafeMethod &&
        context.Request.Path.StartsWithSegments("/api"))
    {
        var antiforgery = context.RequestServices
            .GetRequiredService<IAntiforgery>();

        try
        {
            await antiforgery.ValidateRequestAsync(context);
        }
        catch (AntiforgeryValidationException)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;

            await context.Response.WriteAsJsonAsync(new
            {
                error = "A valid CSRF token is required."
            });

            return;
        }
    }

    await next();
});

app.UseAuthorization();

app.MapAntiforgeryEndpoints();
app.MapAuthEndpoints();
app.MapExpenseEndpoints();
app.MapFamilyEndpoints();

app.Run();

public partial class Program;
