using AutoInsurance.Domain.Quote;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoInsurance.Migrations.Configurations;

public class QuoteCoverageConfiguration : IEntityTypeConfiguration<QuoteCoverage>
{
    public void Configure(EntityTypeBuilder<QuoteCoverage> builder)
    {
        builder.HasKey(qc => new { qc.QuoteId, qc.CoverageTypeId });
        builder.Property(qc => qc.LimitOption).HasMaxLength(50).IsRequired();
        builder.Property(qc => qc.Deductible).HasColumnType("decimal(10,2)");
        builder.Property(qc => qc.AnnualPremium).HasColumnType("decimal(10,2)");
        builder.HasOne(qc => qc.CoverageType).WithMany().HasForeignKey(qc => qc.CoverageTypeId).OnDelete(DeleteBehavior.Restrict);
    }
}
