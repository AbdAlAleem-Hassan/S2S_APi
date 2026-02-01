using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Text;

namespace S2S.Shared.DataTransferObjects.V1.TranslationDTOs
{
	public class SignToTextRequest
	{
		[FromForm(Name = "video_file")]
		public IFormFile VideoFile { get; set; }

		[FromForm(Name = "language")]
		public string Language { get; set; } = "ar"; // القيمة الافتراضية

		[FromForm(Name = "include_audio")]
		public bool IncludeAudio { get; set; } = false; // القيمة الافتراضية
	}
}
