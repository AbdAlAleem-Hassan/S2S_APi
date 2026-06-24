using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using S2S.Domain.Entities.IdentityModule;
using System;
using System.Collections.Generic;
using System.Text;

namespace S2S.Persistence.Configrations
{
    internal class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
    {
        public void Configure(EntityTypeBuilder<ApplicationUser> builder)
        {
            builder.ToTable("Users");
            builder.Property(u=> u.UserName).IsRequired().HasMaxLength(100);
            builder.Property(u => u.UserType).HasColumnName("Type").IsRequired();
            builder.Property(u=> u.SignLanguage).IsRequired();
            builder.Property(u => u.IsActive).HasDefaultValue(true);
            builder.Property(u => u.CreatedAt).IsRequired();
            builder.Property(u => u.LastLoginAt).IsRequired(false);
            builder.Property(u => u.UserType).HasConversion<string>();
            builder.Property(u=> u.SignLanguage).HasConversion<string>();

            builder.Property(u => u.SubscriptionTier).HasConversion<int>();

            builder.OwnsOne(u => u.Address, address =>
            {
                address.ToTable("UserAddresses");
                address.WithOwner().HasForeignKey("UserId");
                address.HasKey("UserId");
                address.Property(a => a.City).HasColumnName("City").HasMaxLength(100);
                address.Property(a => a.Street).HasColumnName("Street").HasMaxLength(100);
                address.Property(a => a.Country).HasColumnName("Country").HasMaxLength(100);
            });
            builder.Property(u => u.DateOfBirth)
                   .HasColumnType("date");
        }
    }
}
