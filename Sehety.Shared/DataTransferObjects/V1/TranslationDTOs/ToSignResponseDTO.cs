using System;
using System.Collections.Generic;
using System.Text;

namespace S2S.Shared.DataTransferObjects.V1.TranslationDTOs
{
	public class ToSignResponseDTO
	{
		public string session_id { get; set; }
		public string status { get; set; }
		public ToSignTranslation translation { get; set; }
	}

	public class ToSignTranslation
	{
		public string video_url { get; set; } 
		public double duration { get; set; }
		public string original_text { get; set; }
	}
}
