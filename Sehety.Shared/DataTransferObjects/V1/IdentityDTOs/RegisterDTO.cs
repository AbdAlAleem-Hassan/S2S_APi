using S2S.Domain.Entities.Enums;
using System.ComponentModel.DataAnnotations;

using S2S.Shared.Attributes;

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
        [NoHtml]
        string DisplayName,

        DateOnly? DateOfBirth,

        [Required(ErrorMessage = "Username is required")]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 50 characters")]
        [RegularExpression(@"^[a-zA-Z0-9_]+$", ErrorMessage = "Username can only contain letters, numbers, and underscores")]
        [NoHtml]
        string UserName,

        [Required(ErrorMessage = "Password is required")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be between 8 and 100 characters")]
        [RegularExpression(
            @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&#])[A-Za-z\d@$!%*?&#]{8,}$",
            ErrorMessage = "Password must contain at least one uppercase, one lowercase, one digit, and one special character (@$!%*?&#)")]
        string Password,

        [Required(ErrorMessage = "Phone number is required")]
        [StringLength(11, MinimumLength = 11, ErrorMessage = "Phone number must be exactly 11 digits")]
        [RegularExpression(@"^01[0125][0-9]{8}$", ErrorMessage = "Invalid Egyptian phone number. Must start with 010, 011, 012, or 015")]
        string PhoneNumber,

        [Required(ErrorMessage = "User type is required")]
        [EnumDataType(typeof(UserType), ErrorMessage = "Invalid user type")]
        UserType UserType,

        bool UsesSignLanguage,

        [EnumDataType(typeof(SignLanguage), ErrorMessage = "Invalid sign language")]
        SignLanguage? SignLanguage
    );
}
