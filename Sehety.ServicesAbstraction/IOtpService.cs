using S2S.Shared.CommonResult;
using S2S.Shared.DataTransferObjects.V1.IdentityDTOs;

namespace S2S.ServicesAbstraction
{
    public interface IOtpService
    {
        Task<Result<UserDTO>> VerifyOtpAsync(VerifyOtpDTO verifyOtpDTO);
        Task<Result> ResendOtpAsync(string email);
    }
}
