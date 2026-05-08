using Microsoft.AspNetCore.Http;
using S2S.Shared.CommonResult;
using S2S.Shared.DataTransferObjects.V1.IdentityDTOs;

namespace S2S.ServicesAbstraction
{
    public interface IProfileService
    {
        Task<Result<UserDTO>> GetUserByEmailAsync(string email);
        Task<Result> UpdateFcmTokenAsync(string email, string fcmToken);
        Task<Result<UpdateProfileResponseDTO>> UpdateProfileAsync(string userId, UpdateProfileDTO updateProfileDTO, CancellationToken cancellationToken = default);
        Task<Result> ChangePasswordAsync(string userId, ChangePasswordDTO changePasswordDTO);
        Task<Result> RequestEmailChangeAsync(string userId, ChangeEmailDTO changeEmailDTO);
        Task<Result> ConfirmEmailChangeAsync(string userId, ConfirmEmailChangeDTO confirmEmailChangeDTO);
        Task<Result<string>> UploadProfileImageAsync(string userId, IFormFile image, string storagePath);
    }
}
