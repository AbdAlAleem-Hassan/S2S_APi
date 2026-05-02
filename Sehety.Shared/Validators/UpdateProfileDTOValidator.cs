using FluentValidation;
using S2S.Shared.DataTransferObjects.V1.IdentityDTOs;

namespace S2S.Shared.Validators
{
	public class UpdateProfileDTOValidator : AbstractValidator<UpdateProfileDTO>
	{
		public UpdateProfileDTOValidator()
		{
			RuleFor(x => x.DisplayName)
				.NotEmpty().WithMessage("Display Name cannot be empty.")
				.MaximumLength(100).WithMessage("Display Name is too long.");

			RuleFor(x => x.PhoneNumber)
				.Matches(@"^\d{10,15}$").WithMessage("Invalid phone number format.")
				.When(x => !string.IsNullOrEmpty(x.PhoneNumber));
		}
	}
	
}
