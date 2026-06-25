using AutoInsurance.Domain.Quote;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoInsurance.Migrations.Configurations;

public class QuoteConfiguration : IEntityTypeConfiguration<Quote>
{
    public void Configure(EntityTypeBuilder<Quote> builder)
    {
        builder.HasKey(q => q.Id);
        builder.Property(q => q.QuoteNumber).HasMaxLength(20).IsRequired();
        builder.HasIndex(q => q.QuoteNumber).IsUnique();
        builder.Property(q => q.Status).HasMaxLength(20).IsRequired();
        builder.Property(q => q.ZipCode).HasMaxLength(10).IsRequired();
        builder.Property(q => q.SessionTokenHash).HasMaxLength(64);

        builder.HasMany(q => q.Drivers).WithOne(d => d.Quote).HasForeignKey(d => d.QuoteId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(q => q.Vehicles).WithOne(v => v.Quote).HasForeignKey(v => v.QuoteId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(q => q.Coverages).WithOne(c => c.Quote).HasForeignKey(c => c.QuoteId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(q => q.Draft).WithOne(d => d.Quote).HasForeignKey<QuoteDraft>(d => d.QuoteId).OnDelete(DeleteBehavior.Cascade);
    }
}
