using System.ComponentModel.DataAnnotations;

namespace S2S.Shared.DataTransferObjects.V1.IdentityDTOs
{
	public record LoginDTO([EmailAddress]string Email, string Password);
}
