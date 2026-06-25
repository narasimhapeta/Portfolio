using AutoInsurance.Domain.Policy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoInsurance.Migrations.Configurations;

public class PolicyConfiguration : IEntityTypeConfiguration<Policy>
{
    public void Configure(EntityTypeBuilder<Policy> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.PolicyNumber).HasMaxLength(20).IsRequired();
        builder.HasIndex(p => p.PolicyNumber).IsUnique();
        builder.Property(p => p.Status).HasMaxLength(20).IsRequired();
        builder.Property(p => p.TotalAnnualPremium).HasColumnType("decimal(10,2)");

        builder.HasMany(p => p.Drivers).WithOne(d => d.Policy).HasForeignKey(d => d.PolicyId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(p => p.Vehicles).WithOne(v => v.Policy).HasForeignKey(v => v.PolicyId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(p => p.Coverages).WithOne(c => c.Policy).HasForeignKey(c => c.PolicyId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(p => p.Endorsements).WithOne(e => e.Policy).HasForeignKey(e => e.PolicyId).OnDelete(DeleteBehavior.Cascade);
    }
}
