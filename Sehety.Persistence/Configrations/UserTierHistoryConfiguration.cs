using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using S2S.Domain.Entities.IdentityModule;
using S2S.Domain.Entities.Usage;

namespace S2S.Persistence.Configrations
{
    internal class UserTierHistoryConfiguration : IEntityTypeConfiguration<UserTierHistory>
    {
        public void Configure(EntityTypeBuilder<UserTierHistory> builder)
        {
            builder.ToTable("UserTierHistories");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id)
                .HasDefaultValueSql("NEWSEQUENTIALID()");

            builder.Property(x => x.OldTier)
                .IsRequired();

            builder.Property(x => x.NewTier)
                .IsRequired();

            builder.Property(x => x.ChangedAt)
                .HasDefaultValueSql("GETUTCDATE()");

            builder.Property(x => x.IpAddress)
                .HasMaxLength(45);

            builder.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(x => x.ChangedByUserId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction);
        }
    }
}
