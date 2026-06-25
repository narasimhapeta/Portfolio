using AutoInsurance.Domain.Document;
using AutoInsurance.Domain.Policy;
using AutoInsurance.Migrations;
using Microsoft.EntityFrameworkCore;

namespace AutoInsurance.CustomerService.Infrastructure.Persistence;

public class CustomerServiceDbContext : DbContext
{
    public CustomerServiceDbContext(DbContextOptions<CustomerServiceDbContext> options) : base(options) { }

    public DbSet<Policy> Policies => Set<Policy>();
    public DbSet<PolicyDriver> PolicyDrivers => Set<PolicyDriver>();
    public DbSet<PolicyVehicle> PolicyVehicles => Set<PolicyVehicle>();
    public DbSet<PolicyCoverage> PolicyCoverages => Set<PolicyCoverage>();
    public DbSet<Endorsement> Endorsements => Set<Endorsement>();
    public DbSet<RenewalRequest> RenewalRequests => Set<RenewalRequest>();
    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();
    public DbSet<Document> Documents => Set<Document>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MasterDbContext).Assembly);
    }
}
