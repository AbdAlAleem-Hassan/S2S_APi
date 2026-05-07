using FluentValidation;
using S2S.Shared.Constants;
using S2S.Shared.DataTransferObjects.V1.IdentityDTOs;

namespace S2S.Shared.Validators
{
	public class RegisterDTOValidator : AbstractValidator<RegisterDTO>
	{
		public RegisterDTOValidator()
		{
			RuleFor(x => x.Email)
				.NotEmpty().WithMessage("Email is required.")
				.EmailAddress().WithMessage("A valid email is required.")
				.MaximumLength(ValidationDefaults.MaxEmailLength).WithMessage($"Email cannot exceed {ValidationDefaults.MaxEmailLength} characters.")
				.Matches(@"^[^<>&'""\\\/;`]*$").WithMessage("Email contains forbidden characters.");

			RuleFor(x => x.DisplayName)
				.NotEmpty().WithMessage("Display Name is required.")
				.MaximumLength(ValidationDefaults.MaxDisplayNameLength).WithMessage($"Display Name cannot exceed {ValidationDefaults.MaxDisplayNameLength} characters.");

			RuleFor(x => x.UserName)
				.NotEmpty().WithMessage("Username is required.")
				.MinimumLength(ValidationDefaults.MinUserNameLength).WithMessage($"Username must be at least {ValidationDefaults.MinUserNameLength} characters long.")
				.MaximumLength(ValidationDefaults.MaxUserNameLength).WithMessage($"Username cannot exceed {ValidationDefaults.MaxUserNameLength} characters.")
				.Matches(@"^[a-zA-Z0-9._-]+$").WithMessage("Username can only contain letters, numbers, dots, hyphens, and underscores.");

			RuleFor(x => x.Password)
				.NotEmpty().WithMessage("Password is required.")
				.MinimumLength(AuthDefaults.PasswordMinLength).WithMessage($"Password must be at least {AuthDefaults.PasswordMinLength} characters long.")
				.MaximumLength(ValidationDefaults.PasswordMaxLength).WithMessage($"Password cannot exceed {ValidationDefaults.PasswordMaxLength} characters.")
				.Matches(@"[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
				.Matches(@"[a-z]").WithMessage("Password must contain at least one lowercase letter.")
				.Matches(@"[0-9]").WithMessage("Password must contain at least one number.")
				.Matches(@"[\!\?\*\.#@\$%\^&\(\)_\+\-=\[\]\{\};:'""<>,./\\]").WithMessage("Password must contain at least one special character.")
				.Matches(@"^[^<>]*$").WithMessage("Password cannot contain HTML tags.");

			RuleFor(x => x.PhoneNumber)
				.NotEmpty().WithMessage("Phone number is required.")
				.Matches(ValidationDefaults.PhoneRegex).WithMessage(ValidationDefaults.PhoneErrorMessage);

			RuleFor(x => x.DateOfBirth)
				.NotEmpty()
				.WithMessage("Date of birth is required.")
				.Must(BeAValidAge).WithMessage("Age must be between 15 and 80 years.");

			RuleFor(x => x.UserType)
				.NotEmpty().WithMessage("User Type is required.");

			When(x => x.UsesSignLanguage, () =>
			{
				RuleFor(x => x.SignLanguage)
				.NotEmpty().WithMessage("Sign Language must be specified if 'Uses Sign Language' is true.");
			});
		}
		private bool BeAValidAge(DateOnly? dateOfBirth)
		{
			if (!dateOfBirth.HasValue) return false;

			var today = DateOnly.FromDateTime(DateTime.Today);
			var age = today.Year - dateOfBirth.Value.Year;

			// تقليل العمر بسنة إذا لم يأتِ يوم ميلاده بعد في السنة الحالية
			if (dateOfBirth.Value > today.AddYears(-age)) age--;

			return age >= 15 && age <= 80;
		}
	}
}
