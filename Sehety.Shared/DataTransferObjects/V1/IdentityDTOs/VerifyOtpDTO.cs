using System.ComponentModel.DataAnnotations;
using S2S.Shared.Attributes;

namespace S2S.Shared.DataTransferObjects.V1.IdentityDTOs
{
    public record VerifyOtpDTO(
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        [StringLength(256, ErrorMessage = "Email cannot exceed 256 characters")]
        [NoHtml]
        string Email,

        [Required(ErrorMessage = "OTP is required")]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "OTP must be exactly 6 characters")]
        [RegularExpression(@"^\d{6}$", ErrorMessage = "OTP must contain only 6 digits")]
        string Otp
    );
}
