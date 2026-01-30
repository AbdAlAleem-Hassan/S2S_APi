using FluentValidation;
using S2S.Shared.DataTransferObjects.V1.IdentityDTOs;
using System;
using System.Collections.Generic;
using System.Text;

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

			// Profile Image Validation
			RuleFor(x => x.ProfileImage)
				.Must(file => file == null || file.Length <= 2 * 1024 * 1024)
				.WithMessage("Image size must be less than 2MB.")
				.Must(file => file == null || IsValidImageExtension(file.FileName))
				.WithMessage("Only .jpg, .jpeg, and .png files are allowed.");
		}

		private bool IsValidImageExtension(string fileName)
		{
			var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
			var extension = Path.GetExtension(fileName).ToLower();
			return allowedExtensions.Contains(extension);
		}
	}
	
}
