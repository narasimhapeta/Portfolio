using AutoInsurance.Domain.Policy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoInsurance.Migrations.Configurations;

public class UserAccountConfiguration : IEntityTypeConfiguration<UserAccount>
{
    public void Configure(EntityTypeBuilder<UserAccount> builder)
    {
        builder.HasKey(u => u.Id);
        builder.Property(u => u.B2CObjectId).HasMaxLength(100).IsRequired();
        builder.HasIndex(u => u.B2CObjectId).IsUnique();
        builder.Property(u => u.Email).HasMaxLength(200).IsRequired();
        builder.HasOne(u => u.Policy).WithMany().HasForeignKey(u => u.PolicyId).OnDelete(DeleteBehavior.Restrict);
    }
}
