using System;
using System.Collections.Generic;
using System.Text;

namespace S2S.Shared.DataTransferObjects.V1.TranslationDTOs
{
	public class TranslationHistoryResponseDTO
	{
		public int Id { get; set; }
		public string? ArabicInputText { get; set; }
		public string? VideoUrl { get; set; }
		public string? PoseUrl { get; set; }
		public string? SigmlContent { get; set; }
		public string? AudioUrl { get; set; }
		public DateTime CreatedAt { get; set; }
	}
}
