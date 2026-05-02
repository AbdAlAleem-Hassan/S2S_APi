using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel;

namespace S2S.Shared.DataTransferObjects.V1.TranslationDTOs
{
	public class AudioToSignRequest
	{
		[FromForm(Name = "audio_file")]
		public IFormFile AudioFile { get; set; } = default!;
		[FromForm(Name = "avatar")]
		[DefaultValue("default")]
		public string Avatar { get; set; } = "default";
		[FromForm(Name = "speed")]
		[DefaultValue("1.0")]
		public string Speed { get; set; } = "1.0";
		[FromForm(Name = "output_format")]
		[DefaultValue("pose")]
		public string OutputFormat { get; set; } = "pose"; 
	}
}
