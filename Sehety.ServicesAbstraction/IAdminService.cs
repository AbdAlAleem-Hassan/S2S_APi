using S2S.Shared.CommonResult;
using S2S.Shared.DataTransferObjects.V1.AdminDTOs;

namespace S2S.ServicesAbstraction
{
	public interface IAdminService
	{
		Task<Result<IEnumerable<DashUserDto>>> GetAllUsersAsync(string currentUserId);
		Task<Result<string>> ToggleUserLockStatusAsync(string userId);
		Task<Result<string>> ToggleUserUnlimitedStatusAsync(string userId);
	}
}
