using AutoInsurance.Domain.Policy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoInsurance.Migrations.Configurations;

public class PolicyVehicleConfiguration : IEntityTypeConfiguration<PolicyVehicle>
{
    public void Configure(EntityTypeBuilder<PolicyVehicle> builder)
    {
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Make).HasMaxLength(50).IsRequired();
        builder.Property(v => v.Model).HasMaxLength(50).IsRequired();
        builder.Property(v => v.VIN).HasMaxLength(17).IsRequired();
        builder.Property(v => v.PrimaryUse).HasMaxLength(20).IsRequired();
    }
}
