using S2S.Shared.Attributes;
using System.ComponentModel.DataAnnotations;

namespace S2S.Shared.DataTransferObjects.V1.IdentityDTOs
{
    public record ChangePasswordDTO(
        [Required(ErrorMessage = "Current password is required")]
        string CurrentPassword,

        [Required(ErrorMessage = "New password is required")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be between 8 and 100 characters")]
        [NoHtml]
        string NewPassword,

        [Required(ErrorMessage = "Confirm password is required")]
        string ConfirmNewPassword
    );
}
