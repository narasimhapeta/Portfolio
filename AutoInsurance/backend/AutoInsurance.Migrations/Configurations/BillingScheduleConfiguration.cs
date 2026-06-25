using AutoInsurance.Domain.Payment;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoInsurance.Migrations.Configurations;

public class BillingScheduleConfiguration : IEntityTypeConfiguration<BillingSchedule>
{
    public void Configure(EntityTypeBuilder<BillingSchedule> builder)
    {
        builder.HasKey(b => b.PolicyId);
        builder.Property(b => b.Frequency).HasMaxLength(20).IsRequired();
    }
}
