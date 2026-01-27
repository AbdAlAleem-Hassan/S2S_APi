using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using S2S.ServicesAbstraction;
using S2S.Shared.DataTransferObjects.V1.IdentityDTOs;
using System.Security.Claims;

namespace S2S.Presentation.Controllers.V1
{
	[ApiVersion("1.0")]
	[Route("api/[controller]")]
	[Route("api/v{version:apiVersion}/[controller]")]
	public class AuthenticationController : ApiBaseController
	{
		private readonly IAuthenticationService _authenticationService;

		public AuthenticationController(IAuthenticationService authenticationService)
		{
			_authenticationService = authenticationService;
		}

		//POST baseUrl/api/Authentication/Login
		[HttpPost("Login")]
		public async Task<ActionResult<UserDTO>> Login(LoginDTO loginDTO)
		{
			var result = await _authenticationService.LoginAsync(loginDTO);
			return HandleRequest(result);
		}

		//POST baseUrl/api/Authentication/Register
		[HttpPost("Register")]
		public async Task<ActionResult<UserDTO>> Register(RegisterDTO registerDTO)
		{
			var result = await _authenticationService.RegisterAsync(registerDTO);
			return HandleRequest(result);
		}

		[Authorize(Roles ="Admin")]
		[HttpGet("EmailExists")]
		public async Task<ActionResult<bool>> CheckEmail(string email)
		{
			var exists = await _authenticationService.CheckEmailAsync(email);
			return Ok(exists);
		}

		[Authorize]
		[HttpGet("CurrentUser")]
		public async Task<ActionResult<UserDTO>> GetCurrentUser()
		{
			var Email = User.FindFirstValue(ClaimTypes.Email);
			var Result = await _authenticationService.GetUserByEmailAsync(Email!);
			return HandleRequest(Result);
		}
	}
}
