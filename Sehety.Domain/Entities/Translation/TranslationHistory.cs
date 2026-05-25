using S2S.Domain.Entities.IdentityModule;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace S2S.Domain.Entities.Translation
{
	public class TranslationHistory
	{
		[Key]
		public int Id { get; set; }

		[Required]
		public string UserId { get; set; } 

		[ForeignKey(nameof(UserId))]
		public ApplicationUser User { get; set; }

		public string? ArabicInputText { get; set; }
		public string? VideoUrl { get; set; }
		public string? PoseUrl { get; set; }
		public string? SigmlContent { get; set; }
		public string? AudioUrl { get; set; }

		public DateTime CreatedAt { get; set; }
	}
}
