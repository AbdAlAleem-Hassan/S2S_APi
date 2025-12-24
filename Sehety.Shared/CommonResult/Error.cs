using System;
using System.Collections.Generic;
using System.Text;

namespace S2S.Shared.CommonResult
{
	public class Error
	{
		public string Code { get; }
		public string Description { get; }
		public ErrorType ErrorType { get; }
		private Error(string code, string description, ErrorType errorType)
		{
			Code = code;
			Description = description;
			ErrorType = errorType;
		}

		public static Error Failure(string code = "General.Failure", string description = "A General Failure Has Occured")
			=> new Error(code, description, ErrorType.Failure);
		public static Error Validation(string code = "General.Validation", string description = "A Validation Error Has Occured")
			=> new Error(code, description, ErrorType.Validation);
		public static Error NotFound(string code = "General.NotFound", string description = "The Requested Resource Was Not Found")
			=> new Error(code, description, ErrorType.NotFound);

		public static Error Unauthorized(string code = "General.Unauthorized", string description = "You Are Not Authorized To Access This Resource")
			=> new Error(code, description, ErrorType.Unauthorized);

		public static Error Forbidden(string code = "General.Forbidden", string description = "You Do Not Have Permission To Access This Resource")
			=> new Error(code, description, ErrorType.Forbidden);

		public static Error InvalidCredentails(string code = "General.InvalidCredentails", string description = "The Provided Credentails Are Invalid")
			=> new Error(code, description, ErrorType.InvalidCredentails);



	}
}
