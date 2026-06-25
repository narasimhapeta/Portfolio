using AutoInsurance.Domain.Quote;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoInsurance.Migrations.Configurations;

public class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Make).HasMaxLength(50).IsRequired();
        builder.Property(v => v.Model).HasMaxLength(50).IsRequired();
        builder.Property(v => v.VIN).HasMaxLength(17).IsRequired();
        builder.Property(v => v.PrimaryUse).HasMaxLength(20).IsRequired();
    }
}
