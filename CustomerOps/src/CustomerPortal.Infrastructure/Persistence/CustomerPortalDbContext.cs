using CustomerPortal.Domain;
using Microsoft.EntityFrameworkCore;

namespace CustomerPortal.Infrastructure.Persistence;

public class CustomerPortalDbContext(DbContextOptions<CustomerPortalDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>(entity =>
        {
            entity.ToTable("Customers");
            entity.HasKey(c => c.Id);
            entity.Property(c => c.FirstName).HasMaxLength(100).IsRequired();
            entity.Property(c => c.LastName).HasMaxLength(100).IsRequired();
            entity.Property(c => c.Email).HasMaxLength(256).IsRequired();
            entity.Property(c => c.Phone).HasMaxLength(30).IsRequired();
            entity.Property(c => c.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.HasIndex(c => c.Email);
            entity.HasIndex(c => c.LastName);
        });
    }
}
