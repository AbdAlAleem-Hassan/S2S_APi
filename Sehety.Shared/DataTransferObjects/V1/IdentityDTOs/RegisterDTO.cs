using S2S.Domain.Entities.Enums;
using System.ComponentModel.DataAnnotations;

namespace S2S.Shared.DataTransferObjects.V1.IdentityDTOs
{
    public record RegisterDTO(
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        [StringLength(256, ErrorMessage = "Email cannot exceed 256 characters")]
        string Email,

        [Required(ErrorMessage = "Display name is required")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Display name must be between 2 and 100 characters")]
        [RegularExpression(@"^[\p{L}\s]+$", ErrorMessage = "Display name can only contain letters and spaces")]
        string DisplayName,

        DateOnly? DateOfBirth,

        [Required(ErrorMessage = "Username is required")]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 50 characters")]
        [RegularExpression(@"^[a-zA-Z0-9_]+$", ErrorMessage = "Username can only contain letters, numbers, and underscores")]
        string UserName,

        [Required(ErrorMessage = "Password is required")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be between 8 and 100 characters")]
        [RegularExpression(
            @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{8,}$",
            ErrorMessage = "Password must contain at least one uppercase, one lowercase, one digit, and one special character (@$!%*?&)")]
        string Password,

        [Phone(ErrorMessage = "Invalid phone number format")]
        [StringLength(20, ErrorMessage = "Phone number cannot exceed 20 characters")]
        string PhoneNumber,

        [Required(ErrorMessage = "User type is required")]
        UserType UserType,

        bool UsesSignLanguage,

        SignLanguage? SignLanguage
    );
}
