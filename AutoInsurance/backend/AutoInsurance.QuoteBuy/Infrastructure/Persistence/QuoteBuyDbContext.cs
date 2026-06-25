using AutoInsurance.Domain.Quote;
using AutoInsurance.Migrations;
using Microsoft.EntityFrameworkCore;

namespace AutoInsurance.QuoteBuy.Infrastructure.Persistence;

public class QuoteBuyDbContext : DbContext
{
    public QuoteBuyDbContext(DbContextOptions<QuoteBuyDbContext> options) : base(options) { }

    public DbSet<Quote> Quotes => Set<Quote>();
    public DbSet<QuoteDraft> QuoteDrafts => Set<QuoteDraft>();
    public DbSet<Driver> Drivers => Set<Driver>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<CoverageType> CoverageTypes => Set<CoverageType>();
    public DbSet<QuoteCoverage> QuoteCoverages => Set<QuoteCoverage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MasterDbContext).Assembly);
    }
}
