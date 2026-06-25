using AutoInsurance.Domain.Policy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoInsurance.Migrations.Configurations;

public class PolicyDriverConfiguration : IEntityTypeConfiguration<PolicyDriver>
{
    public void Configure(EntityTypeBuilder<PolicyDriver> builder)
    {
        builder.HasKey(d => d.Id);
        builder.Property(d => d.DriverType).HasMaxLength(20).IsRequired();
        builder.Property(d => d.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(d => d.LastName).HasMaxLength(100).IsRequired();
        builder.Property(d => d.LicenseNumber).HasMaxLength(50).IsRequired();
        builder.Property(d => d.LicenseState).HasMaxLength(2).IsRequired();
    }
}
