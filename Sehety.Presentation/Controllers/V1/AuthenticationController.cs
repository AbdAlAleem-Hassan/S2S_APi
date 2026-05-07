using Asp.Versioning;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using S2S.Presentation.Filters;
using S2S.ServicesAbstraction;
using S2S.Shared.Constants;
using S2S.Shared.DataTransferObjects.V1.FirebaseDTOs;
using S2S.Shared.DataTransferObjects.V1.GoogleIdentity;
using S2S.Shared.DataTransferObjects.V1.IdentityDTOs;
using S2S.Shared.Helpers;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace S2S.Presentation.Controllers.V1
{
	[ApiVersion("1.0")]
	[Route("api/v{version:apiVersion}/Auth")]
    [EnableRateLimiting(RateLimitPolicies.AuthLimit)]
	public class AuthenticationController : ApiBaseController
	{
		private readonly IAuthService _authService;
		private readonly IOtpService _otpService;
		private readonly ITokenService _tokenService;
		private readonly IProfileService _profileService;
		private readonly IAntiforgery _antiforgery;

		public AuthenticationController(
			IAuthService authService,
			IOtpService otpService,
			ITokenService tokenService,
			IProfileService profileService,
			IAntiforgery antiforgery)
		{
			_authService = authService;
			_otpService = otpService;
			_tokenService = tokenService;
			_profileService = profileService;
			_antiforgery = antiforgery;
		}

		//POST baseUrl/api/Authentication/Login
		[HttpPost("Login")]
		public async Task<ActionResult<UserDTO>> Login(LoginDTO loginDTO)
		{
			var result = await _authService.LoginAsync(loginDTO);
            if (result.IsSuccess && result.Value.RefreshToken != null)
            {
                // For web clients: set cookie
                SetRefreshTokenCookie(result.Value.RefreshToken);
                // For mobile clients: include refresh token in response body
                return Ok(result.Value);
            }
			return HandleRequest(result);
		}

		/*
		[HttpPost("google-login-manual")]
		public async Task<ActionResult<UserDTO>> GoogleLoginManual([FromBody] GoogleLoginDTO googleLoginDTO)
		{
			var result = await _authenticationService.LoginWithGoogleAsync(googleLoginDTO);

			if (result.IsSuccess && result.Value.RefreshToken != null)
			{
				SetRefreshTokenCookie(result.Value.RefreshToken);
				return Ok(result.Value);
			}

			return HandleRequest(result);
		}
		*/

		[HttpPost("google-login")]
		public async Task<ActionResult<UserDTO>> GoogleLogin([FromBody] FirebaseLoginDTO firebaseLoginDTO)
		{
			var result = await _authService.LoginWithFirebaseAsync(firebaseLoginDTO);

			if (result.IsSuccess && result.Value.RefreshToken != null)
			{
				SetRefreshTokenCookie(result.Value.RefreshToken);
				return Ok(result.Value);
			}

			return HandleRequest(result);
		}


		//POST baseUrl/api/Authentication/Register
        [EnableRateLimiting(RateLimitPolicies.OtpRequestLimit)]
		[HttpPost("Register")]
		public async Task<ActionResult> Register(RegisterDTO registerDTO)
		{
			var result = await _authService.RegisterAsync(registerDTO);
            if (result.IsSuccess)
                return Ok(new { success = true, message = "Verification code sent to your email" });
			return HandleRequest(result);
		}

[EnableRateLimiting(RateLimitPolicies.OtpVerifyLimit)]
        [HttpPost("VerifyEmail")]
        public async Task<ActionResult<UserDTO>> VerifyEmail(VerifyOtpDTO verifyOtpDTO)
        {
            var result = await _otpService.VerifyOtpAsync(verifyOtpDTO);
            if (result.IsSuccess && result.Value.RefreshToken != null)
            {
                SetRefreshTokenCookie(result.Value.RefreshToken);
                return Ok(result.Value with { RefreshToken = null });
            }
            return HandleRequest(result);
        }

        
        [HttpPost("RefreshToken")]
        [ValidateAntiForgeryForWeb]
        public async Task<ActionResult<UserDTO>> RefreshToken([FromBody] RefreshTokenDTO? refreshTokenDTO = null)
        {
            var refreshToken = Request.Cookies[CookieNames.RefreshToken] ?? refreshTokenDTO?.RefreshToken;
            
            if (string.IsNullOrEmpty(refreshToken)) 
                return Unauthorized(new { message = "Refresh token is required" });

            var result = await _tokenService.RefreshTokenAsync(refreshToken);
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
        [ValidateAntiForgeryForWeb]
        public async Task<ActionResult> Logout([FromBody] RefreshTokenDTO? refreshTokenDTO = null)
        {
            // Support both web (cookie) and mobile (body) clients
            var refreshToken = Request.Cookies[CookieNames.RefreshToken] ?? refreshTokenDTO?.RefreshToken;
            if (!string.IsNullOrEmpty(refreshToken))
            {
                await _tokenService.LogoutAsync(refreshToken);
            }
            Response.Cookies.Delete(CookieNames.RefreshToken);
            return Ok(new { success = true, message = "Logged out successfully" });
        }

        [EnableRateLimiting(RateLimitPolicies.OtpRequestLimit)]
        [HttpPost("ResendOtp")]
        public async Task<ActionResult> ResendOtp([FromQuery] string email)
        {
            if (string.IsNullOrWhiteSpace(email) || email.Length > 256 || !new EmailAddressAttribute().IsValid(email))
                return BadRequest(new { error = "A valid email address is required." });

            var result = await _otpService.ResendOtpAsync(email);
            if (result.IsSuccess)
                return Ok(new { success = true, message = "New verification code sent to your email" });
            return HandleRequest(result);
        }

        [EnableRateLimiting(RateLimitPolicies.OtpRequestLimit)]
        [HttpPost("ForgotPassword")]
        public async Task<ActionResult> ForgotPassword(ForgotPasswordDTO forgotPasswordDTO)
        {
            var result = await _authService.ForgotPasswordAsync(forgotPasswordDTO);
            if (result.IsSuccess)
                return Ok(new { success = true, message = "If your email exists, a reset link has been sent to your email." });
            return HandleRequest(result);
        }

        [EnableRateLimiting(RateLimitPolicies.OtpVerifyLimit)]
        [HttpPost("ResetPassword")]
        public async Task<ActionResult> ResetPassword(ResetPasswordDTO resetPasswordDTO)
        {
            var result = await _authService.ResetPasswordAsync(resetPasswordDTO);
            if (result.IsSuccess)
                return Ok(new { success = true, message = "Password has been reset successfully." });
            return HandleRequest(result);
        }

        [Authorize]
        [EnableRateLimiting(RateLimitPolicies.ChangePasswordLimit)]
        [HttpPost("ChangePassword")]
        public async Task<ActionResult> ChangePassword([FromBody] ChangePasswordDTO changePasswordDTO)
        {
            // User Id comes from the JWT claim (cannot be tampered with)
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { message = "Invalid token or user not authenticated." });

            var result = await _profileService.ChangePasswordAsync(userId, changePasswordDTO);

            if (result.IsSuccess)
            {
                // Invalidate the refresh token cookie for web clients
                Response.Cookies.Delete(CookieNames.RefreshToken);
                return Ok(new { success = true, message = "Password changed successfully. Please log in again." });
            }

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
            Response.Cookies.Append(CookieNames.RefreshToken, refreshToken, cookieOptions);

            // Generate and store anti-forgery token set for web CSRF protection.
            // This sets the XSRF-TOKEN cookie (non-HttpOnly, JS-readable);
            // the client must echo it back via the X-XSRF-TOKEN header.
            _antiforgery.GetAndStoreTokens(HttpContext);
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
			var Result = await _profileService.GetUserByEmailAsync(Email!);
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
			var result = await _profileService.UpdateFcmTokenAsync(email, updateFcmTokenDTO.FcmToken);

			// بنستخدم دالتك الموحدة للرد
			return HandleRequest(result);
		}

        [Authorize]
        [HttpPost("UpdateProfile")]
        public async Task<ActionResult<UpdateProfileResponseDTO>> UpdateProfile(
            [FromBody] UpdateProfileDTO updateProfileDTO,
            CancellationToken cancellationToken)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { message = "Invalid token or user not authenticated." });
            }

            var result = await _profileService.UpdateProfileAsync(userId, updateProfileDTO, cancellationToken);
            if (!result.IsSuccess)
            {
                return HandleRequest(result);
            }

            var profileUrl = UrlRewriter.BuildMediaUrl(HttpContext, result.Value.ProfileImageUrl, "profile");
            var response = result.Value with { ProfileImageUrl = profileUrl };
            return Ok(response);
        }

        [Authorize]
        [HttpPost("ChangeEmail")]
        [EnableRateLimiting(RateLimitPolicies.OtpRequestLimit)]
        public async Task<IActionResult> RequestEmailChange([FromBody] ChangeEmailDTO changeEmailDTO)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { message = "Invalid token or user not authenticated." });

            var result = await _profileService.RequestEmailChangeAsync(userId, changeEmailDTO);
            if (!result.IsSuccess)
                return HandleRequest(result);

            return Ok(new { message = "Verification code sent to your new email address." });
        }

        [Authorize]
        [HttpPost("ConfirmEmailChange")]
        [EnableRateLimiting(RateLimitPolicies.OtpVerifyLimit)]
        public async Task<IActionResult> ConfirmEmailChange([FromBody] ConfirmEmailChangeDTO confirmEmailChangeDTO)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { message = "Invalid token or user not authenticated." });

            var result = await _profileService.ConfirmEmailChangeAsync(userId, confirmEmailChangeDTO);
            if (!result.IsSuccess)
                return HandleRequest(result);

            return Ok(new { message = "Email changed successfully. Please log in again with your new email." });
        }

	}
}
