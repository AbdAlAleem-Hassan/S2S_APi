using System.ComponentModel.DataAnnotations;

namespace S2S.Shared.DataTransferObjects.V1.IdentityDTOs
{
 public record RegisterDTO([EmailAddress] string Email, string DisplayName, string UserName, string Password, [Phone] string PhoneNumber);
}
