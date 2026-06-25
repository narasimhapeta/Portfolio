using AutoInsurance.Domain.Quote;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoInsurance.Migrations.Configurations;

public class QuoteDraftConfiguration : IEntityTypeConfiguration<QuoteDraft>
{
    public void Configure(EntityTypeBuilder<QuoteDraft> builder)
    {
        builder.HasKey(d => d.QuoteId);
        builder.Property(d => d.DraftStateJson).IsRequired();
        builder.Property(d => d.StepReached).IsRequired();
    }
}
