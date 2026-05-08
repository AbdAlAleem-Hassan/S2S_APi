using FluentValidation;
using S2S.Shared.Constants;
using S2S.Shared.DataTransferObjects.V1.IdentityDTOs;

namespace S2S.Shared.Validators
{
    public class ChangeEmailDTOValidator : AbstractValidator<ChangeEmailDTO>
    {
        public ChangeEmailDTOValidator()
        {
            RuleFor(x => x.NewEmail)
                .NotEmpty().WithMessage("New email is required.")
                .EmailAddress().WithMessage("A valid email is required.")
                .MaximumLength(ValidationDefaults.MaxEmailLength).WithMessage($"Email cannot exceed {ValidationDefaults.MaxEmailLength} characters.")
                .Matches(@"^[^<>&'""\\\/;`]*$").WithMessage("Email contains forbidden characters.");

            RuleFor(x => x.CurrentPassword)
                .NotEmpty().WithMessage("Current password is required.")
                .MaximumLength(ValidationDefaults.PasswordMaxLength).WithMessage($"Password cannot exceed {ValidationDefaults.PasswordMaxLength} characters.");
        }
    }
}
