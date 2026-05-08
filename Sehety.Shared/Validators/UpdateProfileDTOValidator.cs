using FluentValidation;
using S2S.Shared.Constants;
using S2S.Shared.DataTransferObjects.V1.IdentityDTOs;

namespace S2S.Shared.Validators
{
	public class UpdateProfileDTOValidator : AbstractValidator<UpdateProfileDTO>
	{
		public UpdateProfileDTOValidator()
		{
			RuleFor(x => x.DisplayName)
				.NotEmpty().WithMessage("Display Name cannot be empty.")
				.MaximumLength(ValidationDefaults.MaxDisplayNameLength).WithMessage($"Display Name cannot exceed {ValidationDefaults.MaxDisplayNameLength} characters.");

			RuleFor(x => x.PhoneNumber)
				.Matches(ValidationDefaults.PhoneRegex).WithMessage(ValidationDefaults.PhoneErrorMessage)
				.When(x => !string.IsNullOrEmpty(x.PhoneNumber));
		}
	}
	
}
