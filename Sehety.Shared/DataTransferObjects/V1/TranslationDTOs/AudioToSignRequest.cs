using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace S2S.Shared.DataTransferObjects.V1.TranslationDTOs
{
	public class AudioToSignRequest
	{
		[FromForm(Name = "audio_file")]
		public IFormFile AudioFile { get; set; }
		[FromForm(Name = "avatar")]
		public string Avatar { get; set; } = "default";
		[FromForm(Name = "speed")]
		public string Speed { get; set; } = "1.0";
	}
}
