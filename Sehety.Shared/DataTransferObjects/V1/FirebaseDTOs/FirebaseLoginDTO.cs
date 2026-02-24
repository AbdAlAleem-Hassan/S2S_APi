using System.ComponentModel.DataAnnotations;

namespace S2S.Shared.DataTransferObjects.V1.FirebaseDTOs
{
	public class FirebaseLoginDTO
	{
		[Required(ErrorMessage = "Firebase ID Token is required")]
		public string IdToken { get; set; } = string.Empty;
	}
}
