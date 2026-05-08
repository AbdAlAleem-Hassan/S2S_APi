using FluentValidation;
using S2S.Shared.Constants;
using S2S.Shared.DataTransferObjects.V1.IdentityDTOs;

namespace S2S.Shared.Validators
{
    public class ForgotPasswordDTOValidator : AbstractValidator<ForgotPasswordDTO>
    {
        public ForgotPasswordDTOValidator()
        {
            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("Email is required.")
                .MaximumLength(ValidationDefaults.MaxEmailLength).WithMessage($"Email cannot exceed {ValidationDefaults.MaxEmailLength} characters.")
                .EmailAddress().WithMessage("Please enter a valid email address.")
                .Matches(@"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$")
                    .WithMessage("Please enter a valid email format.")
                .Matches(@"^[^<>&'""\\\/;`]*$")
                    .WithMessage("Email contains forbidden characters.");
        }
    }
}
