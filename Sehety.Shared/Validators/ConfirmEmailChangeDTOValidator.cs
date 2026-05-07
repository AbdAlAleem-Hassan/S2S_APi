using FluentValidation;
using S2S.Shared.Constants;
using S2S.Shared.DataTransferObjects.V1.IdentityDTOs;

namespace S2S.Shared.Validators
{
    public class ConfirmEmailChangeDTOValidator : AbstractValidator<ConfirmEmailChangeDTO>
    {
        public ConfirmEmailChangeDTOValidator()
        {
            RuleFor(x => x.NewEmail)
                .NotEmpty().WithMessage("New email is required.")
                .EmailAddress().WithMessage("A valid email is required.")
                .MaximumLength(ValidationDefaults.MaxEmailLength).WithMessage($"Email cannot exceed {ValidationDefaults.MaxEmailLength} characters.");

            RuleFor(x => x.Otp)
                .NotEmpty().WithMessage("OTP is required.")
                .Length(AuthDefaults.OtpLength).WithMessage($"OTP must be exactly {AuthDefaults.OtpLength} characters.")
                .Matches($@"^\d{{{AuthDefaults.OtpLength}}}$").WithMessage($"OTP must contain only {AuthDefaults.OtpLength} digits.");
        }
    }
}
