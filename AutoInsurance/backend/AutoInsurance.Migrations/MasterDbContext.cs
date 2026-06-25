using AutoInsurance.Domain.Claims;
using AutoInsurance.Domain.Document;
using AutoInsurance.Domain.Payment;
using AutoInsurance.Domain.Policy;
using AutoInsurance.Domain.Quote;
using Microsoft.EntityFrameworkCore;

namespace AutoInsurance.Migrations;

public class MasterDbContext : DbContext
{
    public MasterDbContext(DbContextOptions<MasterDbContext> options) : base(options) { }

    public DbSet<Quote> Quotes => Set<Quote>();
    public DbSet<QuoteDraft> QuoteDrafts => Set<QuoteDraft>();
    public DbSet<Driver> Drivers => Set<Driver>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<CoverageType> CoverageTypes => Set<CoverageType>();
    public DbSet<QuoteCoverage> QuoteCoverages => Set<QuoteCoverage>();

    public DbSet<Policy> Policies => Set<Policy>();
    public DbSet<PolicyDriver> PolicyDrivers => Set<PolicyDriver>();
    public DbSet<PolicyVehicle> PolicyVehicles => Set<PolicyVehicle>();
    public DbSet<PolicyCoverage> PolicyCoverages => Set<PolicyCoverage>();
    public DbSet<Endorsement> Endorsements => Set<Endorsement>();
    public DbSet<RenewalRequest> RenewalRequests => Set<RenewalRequest>();
    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();

    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();
    public DbSet<BillingSchedule> BillingSchedules => Set<BillingSchedule>();

    public DbSet<Document> Documents => Set<Document>();

    public DbSet<Claim> Claims => Set<Claim>();
    public DbSet<ClaimDocument> ClaimDocuments => Set<ClaimDocument>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MasterDbContext).Assembly);
    }
}
