using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace S2S.Shared.DataTransferObjects.V1.GoogleIdentity
{
	public class GoogleLoginDTO
	{
		[Required(ErrorMessage = "Google Token is required")]
		public string IdToken { get; set; } = string.Empty;
	}
}
