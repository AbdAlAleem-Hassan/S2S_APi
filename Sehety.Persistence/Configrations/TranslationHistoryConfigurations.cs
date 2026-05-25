using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using S2S.Domain.Entities.Translation;
using System;
using System.Collections.Generic;
using System.Text;

namespace S2S.Persistence.Configrations
{
	internal class TranslationHistoryConfigurations : IEntityTypeConfiguration<TranslationHistory>
	{
		public void Configure(EntityTypeBuilder<TranslationHistory> builder)
		{
			builder.Property(t => t.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
		}
	}
}
