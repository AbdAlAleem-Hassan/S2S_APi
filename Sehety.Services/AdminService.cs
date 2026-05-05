using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using S2S.Domain.Entities.IdentityModule;
using S2S.ServicesAbstraction;
using S2S.Shared.CommonResult;
using S2S.Shared.DataTransferObjects.V1.AdminDTOs;

namespace S2S.Services
{
	public class AdminService : IAdminService
	{
		private readonly UserManager<ApplicationUser> _userManager;

		public AdminService(UserManager<ApplicationUser> userManager)
		{
			_userManager = userManager;
		}

		public async Task<Result<IEnumerable<DashUserDto>>> GetAllUsersAsync(string currentUserId)
		{
			var users = await _userManager.Users
				.Where(u => u.Id != currentUserId)
				.ToListAsync();

			var userDtos = users.Select(u => new DashUserDto
			{
				Id = u.Id,
				FirstName = u.UserName,
				Email = u.Email,
				IsLockedOut = u.LockoutEnd != null && u.LockoutEnd > DateTimeOffset.UtcNow,
				LockoutEnd = u.LockoutEnd
			}).ToList();

			return Result<IEnumerable<DashUserDto>>.Ok(userDtos);
		}

		public async Task<Result<string>> ToggleUserLockStatusAsync(string userId)
		{
			var user = await _userManager.FindByIdAsync(userId);
			if (user == null)
				return Error.NotFound("User.NotFound", "User not found.");

			
			if (user.LockoutEnd == null || user.LockoutEnd < DateTimeOffset.UtcNow)
			{
				await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddYears(100));
				return Result<string>.Ok("User Locked Successfully");
			}
			else
			{
				await _userManager.SetLockoutEndDateAsync(user, null);
				return Result<string>.Ok("User Unlocked Successfully");
			}
		}
	}
}
