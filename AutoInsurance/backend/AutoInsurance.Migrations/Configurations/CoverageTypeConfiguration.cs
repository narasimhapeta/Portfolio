using AutoInsurance.Domain.Quote;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoInsurance.Migrations.Configurations;

public class CoverageTypeConfiguration : IEntityTypeConfiguration<CoverageType>
{
    public void Configure(EntityTypeBuilder<CoverageType> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Code).HasMaxLength(30).IsRequired();
        builder.Property(c => c.Description).HasMaxLength(100).IsRequired();
        builder.Property(c => c.MockAnnualRate).HasColumnType("decimal(10,2)");

        builder.HasData(
            new CoverageType { Id = 1, Code = CoverageCode.BodilyInjury,   Description = "Bodily Injury Liability",    MockAnnualRate = 320.00m },
            new CoverageType { Id = 2, Code = CoverageCode.PropertyDamage, Description = "Property Damage Liability",  MockAnnualRate = 180.00m },
            new CoverageType { Id = 3, Code = CoverageCode.Comprehensive,  Description = "Comprehensive",              MockAnnualRate = 140.00m },
            new CoverageType { Id = 4, Code = CoverageCode.Collision,      Description = "Collision",                  MockAnnualRate = 260.00m },
            new CoverageType { Id = 5, Code = CoverageCode.Uninsured,      Description = "Uninsured Motorist",         MockAnnualRate = 100.00m }
        );
    }
}
