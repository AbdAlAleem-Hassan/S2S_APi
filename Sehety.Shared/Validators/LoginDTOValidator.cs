using FluentValidation;
using S2S.Shared.Constants;
using S2S.Shared.DataTransferObjects.V1.IdentityDTOs;

namespace S2S.Shared.Validators
{
	public class LoginDTOValidator : AbstractValidator<LoginDTO>
	{
		public LoginDTOValidator()
		{
			RuleFor(x => x.Email)
				.NotEmpty().WithMessage("Email is required.")
				.EmailAddress().WithMessage("A valid email is required.")
				.MaximumLength(ValidationDefaults.MaxEmailLength).WithMessage($"Email cannot exceed {ValidationDefaults.MaxEmailLength} characters.");

			RuleFor(x => x.Password)
				.NotEmpty().WithMessage("Password is required.")
				.MaximumLength(ValidationDefaults.PasswordMaxLength).WithMessage($"Password cannot exceed {ValidationDefaults.PasswordMaxLength} characters.");
		}
	}
}
