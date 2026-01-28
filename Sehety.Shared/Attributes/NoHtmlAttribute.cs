using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace S2S.Shared.Attributes
{
    public class NoHtmlAttribute : ValidationAttribute
    {
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value is string input && !string.IsNullOrEmpty(input))
            {
                // Regex to detect HTML tags or suspicious characters like <, >
                if (Regex.IsMatch(input, @"<[^>]*>|&[^;]+;|[<>]"))
                {
                    return new ValidationResult("Input cannot contain HTML tags or dangerous characters.");
                }
            }
            return ValidationResult.Success;
        }
    }
}
