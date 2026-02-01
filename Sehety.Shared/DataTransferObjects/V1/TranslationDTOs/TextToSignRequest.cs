using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Text;

namespace S2S.Shared.DataTransferObjects.V1.TranslationDTOs
{
	public class TextToSignRequest
	{
		[FromForm(Name ="text")] 
		public string Text { get; set; }
		[FromForm(Name = "avatar")]
		public string Avatar { get; set; } = "default";
		[FromForm(Name = "speed")] 
	    public	string Speed { get; set; } = "1.0";
	}
}
