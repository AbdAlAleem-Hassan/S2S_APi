using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using S2S.ServicesAbstraction;
using S2S.Shared.DataTransferObjects.V1.FirebaseDTOs;
using S2S.Shared.DataTransferObjects.V1.GoogleIdentity;
using S2S.Shared.DataTransferObjects.V1.IdentityDTOs;
using System.Security.Claims;

namespace S2S.Presentation.Controllers.V1
{
	[ApiVersion("1.0")]
	[Route("api/v{version:apiVersion}/Auth")]
    [EnableRateLimiting("auth-limit")]
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
            if (result.IsSuccess && result.Value.RefreshToken != null)
            {
                // For web clients: set cookie
                SetRefreshTokenCookie(result.Value.RefreshToken);
                // For mobile clients: include refresh token in response body
                return Ok(result.Value);
            }
			return HandleRequest(result);
		}

		[HttpPost("google-login")]
		public async Task<ActionResult<UserDTO>> GoogleLogin([FromBody] GoogleLoginDTO googleLoginDTO)
		{
			// لا حاجة لـ ModelState.IsValid هنا لأن [ApiController] في ApiBaseController تقوم بذلك تلقائياً

			var result = await _authenticationService.LoginWithGoogleAsync(googleLoginDTO);

			if (result.IsSuccess && result.Value.RefreshToken != null)
			{
				// For web clients: set cookie
				SetRefreshTokenCookie(result.Value.RefreshToken);

				// For mobile clients: include refresh token in response body
				return Ok(result.Value);
			}

			// استخدام دالة الـ Base Controller الموحدة للتعامل مع الأخطاء
			return HandleRequest(result);
		}


		//POST baseUrl/api/Authentication/Register
		[HttpPost("Register")]
		public async Task<ActionResult> Register(RegisterDTO registerDTO)
		{
			var result = await _authenticationService.RegisterAsync(registerDTO);
            if (result.IsSuccess)
                return Ok(new { success = true, message = "Verification code sent to your email" });
			return HandleRequest(result);
		}

        [HttpPost("VerifyEmail")]
        public async Task<ActionResult<UserDTO>> VerifyEmail(VerifyOtpDTO verifyOtpDTO)
        {
            var result = await _authenticationService.VerifyOtpAsync(verifyOtpDTO);
            if (result.IsSuccess && result.Value.RefreshToken != null)
            {
                SetRefreshTokenCookie(result.Value.RefreshToken);
                return Ok(result.Value with { RefreshToken = null });
            }
            return HandleRequest(result);
        }

        
        [HttpPost("RefreshToken")]
        public async Task<ActionResult<UserDTO>> RefreshToken([FromBody] RefreshTokenDTO? refreshTokenDTO = null)
        {
            var refreshToken = Request.Cookies["refreshToken"] ?? refreshTokenDTO?.RefreshToken;
            
            if (string.IsNullOrEmpty(refreshToken)) 
                return Unauthorized(new { message = "Refresh token is required" });

            var result = await _authenticationService.RefreshTokenAsync(refreshToken);
            if (result.IsSuccess && result.Value.RefreshToken != null)
            {
                // For web clients: set cookie
                SetRefreshTokenCookie(result.Value.RefreshToken);
                
                // For mobile clients: include refresh token in response body
                // Web clients can ignore it since they use cookies
                return Ok(result.Value);
            }
            return HandleRequest(result);
        }

        [Authorize]
        [HttpPost("Logout")]
        public async Task<ActionResult> Logout([FromBody] RefreshTokenDTO? refreshTokenDTO = null)
        {
            // Support both web (cookie) and mobile (body) clients
            var refreshToken = Request.Cookies["refreshToken"] ?? refreshTokenDTO?.RefreshToken;
            if (!string.IsNullOrEmpty(refreshToken))
            {
                await _authenticationService.LogoutAsync(refreshToken);
            }
            Response.Cookies.Delete("refreshToken");
            return Ok(new { success = true, message = "Logged out successfully" });
        }

        [HttpPost("ResendOtp")]
        public async Task<ActionResult> ResendOtp([FromQuery] string email)
        {
            var result = await _authenticationService.ResendOtpAsync(email);
            if (result.IsSuccess)
                return Ok(new { success = true, message = "New verification code sent to your email" });
            return HandleRequest(result);
        }

        [HttpPost("ForgotPassword")]
        public async Task<ActionResult> ForgotPassword(ForgotPasswordDTO forgotPasswordDTO)
        {
            var result = await _authenticationService.ForgotPasswordAsync(forgotPasswordDTO);
            if (result.IsSuccess)
                return Ok(new { success = true, message = "If your email exists, a reset link has been sent to your email." });
            return HandleRequest(result);
        }

        [HttpPost("ResetPassword")]
        public async Task<ActionResult> ResetPassword(ResetPasswordDTO resetPasswordDTO)
        {
            var result = await _authenticationService.ResetPasswordAsync(resetPasswordDTO);
            if (result.IsSuccess)
                return Ok(new { success = true, message = "Password has been reset successfully." });
            return HandleRequest(result);
        }

        private void SetRefreshTokenCookie(string refreshToken)
        {
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = true, 
                SameSite = SameSiteMode.Strict,
                Expires = DateTime.UtcNow.AddDays(7)
            };
            Response.Cookies.Append("refreshToken", refreshToken, cookieOptions);
        }

/*
		[HttpGet("EmailExists")]
		public async Task<ActionResult<bool>> CheckEmail(string email)
		{
			var exists = await _authenticationService.CheckEmailAsync(email);
			return Ok(exists);
		}
*/

		[Authorize]
		[HttpGet("CurrentUser")]
		public async Task<ActionResult<UserDTO>> GetCurrentUser()
		{
			var Email = User.FindFirstValue(ClaimTypes.Email);
			var Result = await _authenticationService.GetUserByEmailAsync(Email!);
			return HandleRequest(Result);
		}

		[Authorize]
		[HttpPost("UpdateFcmToken")]
		public async Task<ActionResult> UpdateFcmToken([FromBody] UpdateFcmTokenDTO updateFcmTokenDTO)
		{
			// بنجيب إيميل اليوزر من التوكن بتاعه
			var email = User.FindFirstValue(ClaimTypes.Email);

			if (string.IsNullOrEmpty(email))
				return Unauthorized(new { message = "Invalid token or user not logged in." });

			// بنبعت الإيميل والتوكن للـ Service عشان هي اللي تتعامل مع الداتابيس
			var result = await _authenticationService.UpdateFcmTokenAsync(email, updateFcmTokenDTO.FcmToken);

			// بنستخدم دالتك الموحدة للرد
			return HandleRequest(result);
		}

	}
}
