using AutoInsurance.Domain.Policy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoInsurance.Migrations.Configurations;

public class EndorsementConfiguration : IEntityTypeConfiguration<Endorsement>
{
    public void Configure(EntityTypeBuilder<Endorsement> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Type).HasMaxLength(30).IsRequired();
        builder.Property(e => e.Status).HasMaxLength(20).IsRequired();
        builder.Property(e => e.ChangeJson).IsRequired();
    }
}
