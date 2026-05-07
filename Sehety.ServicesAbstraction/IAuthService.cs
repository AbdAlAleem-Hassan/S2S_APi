using S2S.Shared.CommonResult;
using S2S.Shared.DataTransferObjects.V1.FirebaseDTOs;
using S2S.Shared.DataTransferObjects.V1.IdentityDTOs;

namespace S2S.ServicesAbstraction
{
    public interface IAuthService
    {
        Task<Result<UserDTO>> LoginAsync(LoginDTO loginDTO);
        Task<Result> RegisterAsync(RegisterDTO registerDTO);
        Task<Result<UserDTO>> LoginWithFirebaseAsync(FirebaseLoginDTO firebaseLoginDTO);
        Task<Result> ForgotPasswordAsync(ForgotPasswordDTO forgotPasswordDTO);
        Task<Result> ResetPasswordAsync(ResetPasswordDTO resetPasswordDTO);
    }
}
