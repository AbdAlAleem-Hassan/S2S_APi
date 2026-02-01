using System;
using System.Collections.Generic;
using System.Text;

namespace S2S.Shared.DataTransferObjects.V1.TranslationDTOs
{
	public class SignToTextResponseDTO
	{
		public string session_id { get; set; }
		public string status { get; set; }

		// التغيير هنا: استخدام Dictionary لاستقبال أي مفاتيح متغيرة
		public Dictionary<string, object> translation { get; set; }
	}
}
