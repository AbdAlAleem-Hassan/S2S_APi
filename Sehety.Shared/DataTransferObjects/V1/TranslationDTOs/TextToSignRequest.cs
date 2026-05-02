using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace S2S.Shared.DataTransferObjects.V1.TranslationDTOs
{
	public class TextToSignRequest
	{
		[FromForm(Name ="text")] 
		public string Text { get; set; } = string.Empty;
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
