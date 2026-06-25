using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using S2S.Domain.Entities.Usage;

namespace S2S.Persistence.Configrations
{
    internal class UserUsageConfiguration : IEntityTypeConfiguration<UserUsage>
    {
        public void Configure(EntityTypeBuilder<UserUsage> builder)
        {
            builder.HasKey(u => new { u.UserId, u.WindowStart });

            builder.Property(u => u.WindowStart).IsRequired();

            builder.Property(u => u.Count).IsRequired().HasDefaultValue(0);

            builder.Property(u => u.QuotaType).IsRequired();

            builder.ToTable("UserUsages");
        }
    }
}
