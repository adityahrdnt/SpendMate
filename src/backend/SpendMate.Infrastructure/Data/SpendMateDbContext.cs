using Microsoft.EntityFrameworkCore;
using SpendMate.Domain.Entities;

namespace SpendMate.Infrastructure.Data;

public class SpendMateDbContext(
    DbContextOptions<SpendMateDbContext> options)
    : DbContext(options)
{
    public DbSet<Family> Families => Set<Family>();

    public DbSet<User> Users => Set<User>();

    public DbSet<FamilyMembership> FamilyMemberships =>
        Set<FamilyMembership>();

    public DbSet<Expense> Expenses => Set<Expense>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(SpendMateDbContext).Assembly);
    }
}
