using System;
using System.Collections.Generic;
using System.Text;

namespace S2S.Shared.CommonResult
{
	public enum ErrorType
	{
		Failure = 0,
		Validation = 1,
		NotFound = 2,
		Unauthorized = 3,
		Forbidden = 4,
		InvalidCredentails = 5
	}
}
