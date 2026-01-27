using System.ComponentModel.DataAnnotations;
using S2S.Shared.Attributes;

namespace S2S.Shared.DataTransferObjects.V1.IdentityDTOs
{
    public record LoginDTO(
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        [StringLength(256, ErrorMessage = "Email cannot exceed 256 characters")]
        [NoHtml]
        string Email,

        [Required(ErrorMessage = "Password is required")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be between 8 and 100 characters")]
        string Password
    );
}
