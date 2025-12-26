using S2S.Domain.Entities.Enums;
using System.ComponentModel.DataAnnotations;

namespace S2S.Shared.DataTransferObjects.V1.IdentityDTOs
{
 public record RegisterDTO(
     [EmailAddress] string Email, 
     string DisplayName, 
     string UserName, 
     [Required, MinLength(8)] string Password, 
     [Phone] string PhoneNumber,
     [Required]string UserType,
     bool UsesSignLanguage, 
     string SignLanguage );
}
