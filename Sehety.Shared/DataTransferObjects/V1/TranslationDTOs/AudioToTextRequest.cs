using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel;

namespace S2S.Shared.DataTransferObjects.V1.TranslationDTOs
{
	public class AudioToTextRequest
	{
		[FromForm(Name = "audio_file")]
		public IFormFile AudioFile { get; set; } = default!;

		[FromForm(Name = "language")]
		[DefaultValue("ar")]
		public string Language { get; set; } = "ar";
	}
}
