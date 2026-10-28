using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SpendMate.Api.Development;
using SpendMate.Api.Endpoints;
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

if (builder.Environment.IsDevelopment() ||
    builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddScoped<ICurrentUser, DevelopmentCurrentUser>();
}

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

if (app.Environment.IsDevelopment() ||
    app.Environment.IsEnvironment("Testing"))
{
    app.MapExpenseEndpoints();
}

app.Run();

public partial class Program;
