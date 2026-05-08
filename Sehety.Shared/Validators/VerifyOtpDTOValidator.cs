using FluentValidation;
using S2S.Shared.Constants;
using S2S.Shared.DataTransferObjects.V1.IdentityDTOs;

namespace S2S.Shared.Validators
{
    public class VerifyOtpDTOValidator : AbstractValidator<VerifyOtpDTO>
    {
        public VerifyOtpDTOValidator()
        {
            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("Email is required")
                .EmailAddress().WithMessage("Invalid email format")
                .MaximumLength(ValidationDefaults.MaxEmailLength).WithMessage($"Email cannot exceed {ValidationDefaults.MaxEmailLength} characters")
                .Matches(@"^[^<>]*$").WithMessage("Email contains forbidden characters.");

            RuleFor(x => x.Otp)
                .NotEmpty().WithMessage("OTP is required")
                .Length(AuthDefaults.OtpLength).WithMessage($"OTP must be exactly {AuthDefaults.OtpLength} characters")
                .Matches($@"^\d{{{AuthDefaults.OtpLength}}}$").WithMessage($"OTP must contain only {AuthDefaults.OtpLength} digits");
        }
    }
}
