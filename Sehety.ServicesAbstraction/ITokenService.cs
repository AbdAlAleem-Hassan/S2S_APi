using S2S.Domain.Entities.IdentityModule;
using S2S.Shared.CommonResult;
using S2S.Shared.DataTransferObjects.V1.IdentityDTOs;

namespace S2S.ServicesAbstraction
{
    public interface ITokenService
    {
        Task<Result<UserDTO>> RefreshTokenAsync(string refreshToken);
        Task<Result> LogoutAsync(string refreshToken);
        Task<string> CreateAccessTokenAsync(ApplicationUser user);
        Task<UserDTO> MapToUserDTOAsync(ApplicationUser user, string? rawRefreshToken = null);
    }
}
