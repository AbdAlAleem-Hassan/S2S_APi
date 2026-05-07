using FluentValidation;
using S2S.Shared.DataTransferObjects.V1.TranslationDTOs;

namespace S2S.Shared.Validators
{
	public class TextToSignRequestValidator : AbstractValidator<TextToSignRequest> 
	{
		public TextToSignRequestValidator()
		{
			RuleFor(x => x.Text)
				.MaximumLength(200).WithMessage("Text cannot exceed 200 characters.");
		}
	}
}
