using AutoInsurance.Domain.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoInsurance.Migrations.Configurations;

public class ClaimConfiguration : IEntityTypeConfiguration<Claim>
{
    public void Configure(EntityTypeBuilder<Claim> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Description).HasMaxLength(1000).IsRequired();
        builder.Property(c => c.Status).HasMaxLength(20).IsRequired();
        builder.HasMany(c => c.Documents).WithOne(d => d.Claim).HasForeignKey(d => d.ClaimId).OnDelete(DeleteBehavior.Cascade);
    }
}
