using FluentValidation;
using S2S.Shared.Constants;
using S2S.Shared.DataTransferObjects.V1.TranslationDTOs;

namespace S2S.Shared.Validators
{
	public class TextToSignRequestValidator : AbstractValidator<TextToSignRequest>
	{
		public TextToSignRequestValidator()
		{
			RuleFor(x => x.Text)
				.NotEmpty().WithMessage("Text is required for translation.")
				.MaximumLength(ValidationDefaults.MaxTranslationTextLength).WithMessage($"Text cannot exceed {ValidationDefaults.MaxTranslationTextLength} characters.");
		}
	}
}
