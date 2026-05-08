using FluentValidation;
using S2S.Shared.Constants;
using S2S.Shared.DataTransferObjects.V1.IdentityDTOs;

namespace S2S.Shared.Validators
{
    public class ResetPasswordDTOValidator : AbstractValidator<ResetPasswordDTO>
    {
        public ResetPasswordDTOValidator()
        {
            RuleFor(x => x.Token)
                .NotEmpty().WithMessage("Security token is required.");

            RuleFor(x => x.NewPassword)
                .NotEmpty().WithMessage("New password is required.")
                .MinimumLength(AuthDefaults.PasswordMinLength).WithMessage($"Password must be at least {AuthDefaults.PasswordMinLength} characters long.")
                .MaximumLength(ValidationDefaults.PasswordMaxLength).WithMessage($"Password cannot exceed {ValidationDefaults.PasswordMaxLength} characters.")
                .Matches(@"[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
                .Matches(@"[a-z]").WithMessage("Password must contain at least one lowercase letter.")
                .Matches(@"[0-9]").WithMessage("Password must contain at least one number.")
                .Matches(@"[\!\?\*\.#@\$%\^&\(\)_\+\-=\[\]\{\};:'""<>,./\\]").WithMessage("Password must contain at least one special character.")
                .Matches(@"^[^<>]*$").WithMessage("Password cannot contain HTML tags.");

            RuleFor(x => x.ConfirmPassword)
                .NotEmpty().WithMessage("Confirm password is required.")
                .Equal(x => x.NewPassword).WithMessage("Passwords do not match.");
        }
    }
}
