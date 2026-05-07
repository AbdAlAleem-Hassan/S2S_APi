using S2S.Domain.Entities.Enums;

namespace S2S.Shared.DataTransferObjects.V1.IdentityDTOs
{
    public record RegisterDTO(
        string Email,

        string DisplayName,

       
        DateOnly? DateOfBirth,

        string UserName,

        string Password,

        string PhoneNumber,

        UserType UserType,

        bool UsesSignLanguage,

        SignLanguage? SignLanguage
    );
}
