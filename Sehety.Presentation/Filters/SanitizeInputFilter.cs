using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Http;
using System.Text.RegularExpressions;

namespace S2S.Presentation.Filters
{
	public partial class SanitizeInputFilter : ActionFilterAttribute
	{
		private static readonly HashSet<char> DangerousFileNameChars = [.. Path.GetInvalidFileNameChars()];

		private static readonly Regex DangerousPathPattern = DangerousPathRegex();

		[GeneratedRegex(@"\.\.[\\/]|[\0\b\v\f\x1a]")]
		private static partial Regex DangerousPathRegex();

		public override void OnActionExecuting(ActionExecutingContext context)
		{
			foreach (var arg in context.ActionArguments)
			{
				switch (arg.Value)
				{
					case string str:
						if (ContainsDangerousInput(str))
						{
							context.Result = new BadRequestObjectResult(new
							{
								error = "Invalid input detected.",
								parameter = arg.Key
							});
							return;
						}
						break;

					case IFormFile file:
						if (IsDangerousFileName(file.FileName))
						{
							context.Result = new BadRequestObjectResult(new
							{
								error = "Invalid file name.",
								parameter = arg.Key
							});
							return;
						}
						break;

					case IEnumerable<IFormFile> files:
						foreach (var f in files)
						{
							if (IsDangerousFileName(f.FileName))
							{
								context.Result = new BadRequestObjectResult(new
								{
									error = "Invalid file name.",
									parameter = arg.Key
								});
								return;
							}
						}
						break;
				}
			}

			base.OnActionExecuting(context);
		}

		private static bool ContainsDangerousInput(string input)
		{
			if (string.IsNullOrEmpty(input))
				return false;

			if (input.Length > 2000)
				return true;

			if (DangerousPathPattern.IsMatch(input))
				return true;

			if (input.Any(c => char.IsControl(c) && c != '\r' && c != '\n' && c != '\t'))
				return true;

			return false;
		}

		private static bool IsDangerousFileName(string fileName)
		{
			if (string.IsNullOrWhiteSpace(fileName))
				return true;

			if (fileName.Length > 512)
				return true;

			if (DangerousPathPattern.IsMatch(fileName))
				return true;

			if (fileName.Any(c => DangerousFileNameChars.Contains(c)))
				return true;

			if (fileName.Contains("..", StringComparison.Ordinal))
				return true;

			return false;
		}
	}
}
