using FluentValidation;
using S2S.Shared.DataTransferObjects.V1.IdentityDTOs;
using System;
using System.Collections.Generic;
using System.Text;

namespace S2S.Shared.Validators
{
	public class UserDTOValidator : AbstractValidator<UserDTO>
	{
		public UserDTOValidator()
		{
			RuleFor(x => x.Email)
				.NotEmpty().WithMessage("Email is required.")
				.EmailAddress().WithMessage("A valid email is required.");

			RuleFor(x => x.DisplayName)
				.NotEmpty().WithMessage("Display Name is required.")
				.MaximumLength(50).WithMessage("Display Name cannot exceed 50 characters.");
			
			RuleFor(x => x.Token)
				.NotEmpty().WithMessage("Security Token is missing.")
				.MinimumLength(20).WithMessage("Token seems invalidly short.");

		}
	}
}
