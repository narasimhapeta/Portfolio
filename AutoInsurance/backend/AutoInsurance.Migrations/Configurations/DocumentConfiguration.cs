using AutoInsurance.Domain.Document;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoInsurance.Migrations.Configurations;

public class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Type).HasMaxLength(30).IsRequired();
        builder.Property(d => d.BlobUrl).HasMaxLength(500).IsRequired();
    }
}
