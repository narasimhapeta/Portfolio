using AutoInsurance.Domain.Policy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoInsurance.Migrations.Configurations;

public class PolicyCoverageConfiguration : IEntityTypeConfiguration<PolicyCoverage>
{
    public void Configure(EntityTypeBuilder<PolicyCoverage> builder)
    {
        builder.HasKey(pc => new { pc.PolicyId, pc.CoverageTypeId });
        builder.Property(pc => pc.LimitOption).HasMaxLength(50).IsRequired();
        builder.Property(pc => pc.Deductible).HasColumnType("decimal(10,2)");
        builder.Property(pc => pc.AnnualPremium).HasColumnType("decimal(10,2)");
    }
}
