using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using S2S.Shared.CommonResult;

namespace S2S.Presentation.Controllers
{
	[ApiController]
	
	public class ApiBaseController : ControllerBase
	{
		protected IActionResult HandleRequest(Result result)
		{
			if (result.IsSuccess)
				return NoContent();
			else
				return HandleProblem(result.Errors);

		}

		protected ActionResult<TValue> HandleRequest<TValue>(Result<TValue> result)
		{
			if (result.IsSuccess)
				return Ok(result.Value);
			else
				return HandleProblem(result.Errors);

		}

		private ActionResult HandleProblem(IReadOnlyList<Error> errors)
		{
			if(errors.Count == 0)
				return Problem(statusCode: StatusCodes.Status500InternalServerError, title: "An unexpected error occurred.");

			if (errors.All(e => e.ErrorType == ErrorType.Validation))
				return HandleValidationProblem(errors);

			return HandleSingleErrorProblem(errors[0]);
		}

		private ActionResult HandleSingleErrorProblem(Error error)
		{
			return Problem(
				title: error.Code,
				detail: error.Description,
				type: error.ErrorType.ToString(),
				statusCode: MapErrorTypeToStatusCode(error.ErrorType)
				);
		}

		private static int MapErrorTypeToStatusCode(ErrorType errorType) => errorType switch
		{
			ErrorType.NotFound => StatusCodes.Status404NotFound,
			ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
			ErrorType.Forbidden => StatusCodes.Status403Forbidden,
			ErrorType.Validation => StatusCodes.Status400BadRequest,
			ErrorType.InvalidCredentails => StatusCodes.Status401Unauthorized,
			ErrorType.Failure => StatusCodes.Status500InternalServerError,
			_ => StatusCodes.Status500InternalServerError,
		};
	
		private ActionResult HandleValidationProblem(IReadOnlyList<Error> errors)
		{
			var modelState = new ModelStateDictionary();
			foreach (var error in errors)
			{
				modelState.AddModelError(error.Code, error.Description);
			}
			return ValidationProblem(modelState);
		}

	} 
}
