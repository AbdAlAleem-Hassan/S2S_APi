using FluentValidation;
using S2S.Shared.DataTransferObjects.V1.IdentityDTOs;

namespace S2S.Shared.Validators
{
	public class LoginDTOValidator : AbstractValidator<LoginDTO>
	{
		public LoginDTOValidator()
		{
			RuleFor(x => x.Email)
				.NotEmpty().WithMessage("Email is required.")
				.EmailAddress().WithMessage("A valid email is required.");

			RuleFor(x => x.Password)
				.NotEmpty().WithMessage("Password is required.");
		}
	}
}
