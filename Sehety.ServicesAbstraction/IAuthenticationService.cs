using S2S.Shared.CommonResult;
using S2S.Shared.DataTransferObjects.V1.GoogleIdentity;
using S2S.Shared.DataTransferObjects.V1.IdentityDTOs;
using System;
using System.Collections.Generic;
using System.Text;

namespace S2S.ServicesAbstraction
{
	public interface IAuthenticationService
	{
		Task<Result<UserDTO>> LoginAsync(LoginDTO loginDTO);
		Task<Result> RegisterAsync(RegisterDTO registerDTO);
		// Task<bool> CheckEmailAsync(string email);
		Task<Result<UserDTO>> GetUserByEmailAsync(string email);
        Task<Result<UserDTO>> VerifyOtpAsync(VerifyOtpDTO verifyOtpDTO);
        Task<Result<UserDTO>> RefreshTokenAsync(string refreshToken);
        Task<Result> LogoutAsync(string refreshToken);
        Task<Result> ResendOtpAsync(string email);
        Task<Result> ForgotPasswordAsync(ForgotPasswordDTO forgotPasswordDTO);
        Task<Result> ResetPasswordAsync(ResetPasswordDTO resetPasswordDTO);
		Task<Result<UserDTO>> LoginWithGoogleAsync(GoogleLoginDTO googleLoginDTO);
		Task<Result> UpdateFcmTokenAsync(string email, string fcmToken);
		Task<Result> ChangePasswordAsync(string userId, ChangePasswordDTO changePasswordDTO);
	}
}
