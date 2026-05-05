using S2S.Shared.CommonResult;
using S2S.Shared.DataTransferObjects.V1.AdminDTOs;
using System;
using System.Collections.Generic;
using System.Text;

namespace S2S.ServicesAbstraction
{
	public interface IAdminService
	{
		Task<Result<IEnumerable<DashUserDto>>> GetAllUsersAsync(string currentUserId);
		Task<Result<string>> ToggleUserLockStatusAsync(string userId);
	}
}
