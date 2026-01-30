using FluentValidation;
using S2S.Shared.DataTransferObjects.V1.IdentityDTOs;

namespace S2S.Shared.Validators
{
	public class RegisterDTOValidator : AbstractValidator<RegisterDTO>
	{
		public RegisterDTOValidator()
		{
			RuleFor(x => x.Email)
				.NotEmpty().WithMessage("Email is required.")
				.EmailAddress().WithMessage("A valid email is required.");

			RuleFor(x => x.DisplayName)
				.NotEmpty().WithMessage("Display Name is required.")
				.MaximumLength(50).WithMessage("Display Name cannot exceed 50 characters.");

			RuleFor(x => x.UserName)
				.NotEmpty().WithMessage("Username is required.")
				.MinimumLength(3).WithMessage("Username must be at least 3 characters long.");

			RuleFor(x => x.Password)
				.NotEmpty().WithMessage("Password is required.")
				.MinimumLength(8).WithMessage("Password must be at least 8 characters long.")
				.Matches(@"[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
				.Matches(@"[a-z]").WithMessage("Password must contain at least one lowercase letter.")
				.Matches(@"[0-9]").WithMessage("Password must contain at least one number.")
				.Matches(@"[\!\?\*\.]").WithMessage("Password must contain at least one special character (!?*.).");

			RuleFor(x => x.PhoneNumber)
				.NotEmpty().WithMessage("Phone number is required.")
				.Matches(@"^\d{10,15}$").WithMessage("Phone number must be between 10 and 15 digits.");

			RuleFor(x => x.DateOfBirth)
				.LessThan(DateOnly.FromDateTime(DateTime.Now))
				.WithMessage("Date of birth cannot be in the future.")
				.When(x => x.DateOfBirth.HasValue);

			RuleFor(x => x.UserType)
				.NotEmpty().WithMessage("User Type is required.");

			When(x => x.UsesSignLanguage, () =>
			{
				RuleFor(x => x.SignLanguage)
				.NotEmpty().WithMessage("Sign Language must be specified if 'Uses Sign Language' is true.");
			});
		}
	}
}
