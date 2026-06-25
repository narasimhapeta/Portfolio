using AutoInsurance.Domain.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoInsurance.Migrations.Configurations;

public class ClaimDocumentConfiguration : IEntityTypeConfiguration<ClaimDocument>
{
    public void Configure(EntityTypeBuilder<ClaimDocument> builder)
    {
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Type).HasMaxLength(30).IsRequired();
        builder.Property(d => d.BlobUrl).HasMaxLength(500).IsRequired();
    }
}
