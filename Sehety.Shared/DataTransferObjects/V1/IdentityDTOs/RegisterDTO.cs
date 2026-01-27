using S2S.Domain.Entities.Enums;
using System.ComponentModel.DataAnnotations;

namespace S2S.Shared.DataTransferObjects.V1.IdentityDTOs
{
 public record RegisterDTO(
     string Email, 
     string DisplayName,
     DateOnly? DateOfBirth,
     string UserName, 
     string Password, 
     string PhoneNumber,
     string UserType,
     bool UsesSignLanguage, 
     string? SignLanguage 
     );
}
