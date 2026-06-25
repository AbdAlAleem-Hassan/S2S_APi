using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using S2S.Domain.Entities.Enums;
using S2S.Domain.Entities.IdentityModule;
using S2S.Domain.Entities.Usage;
using S2S.Persistence.IdentityData.DbContexts;
using S2S.ServicesAbstraction;
using S2S.Shared.CommonResult;
using S2S.Shared.DataTransferObjects.V1.AdminDTOs;
using S2S.Shared.Helpers;

namespace S2S.Services
{
	public class AdminService : IAdminService
	{
		private readonly UserManager<ApplicationUser> _userManager;
		private readonly S2SIdentityDbContext _db;
		private readonly IEmailService _emailService;
		private readonly ILogger<AdminService> _logger;

		public AdminService(
			UserManager<ApplicationUser> userManager,
			S2SIdentityDbContext db,
			IEmailService emailService,
			ILogger<AdminService> logger)
		{
			_userManager = userManager;
			_db = db;
			_emailService = emailService;
			_logger = logger;
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

		public async Task<Result<string>> SetUserTierAsync(string userId, string tier, string changedByUserId, string? ipAddress)
		{
			var user = await _userManager.FindByIdAsync(userId);
			if (user == null)
				return Error.NotFound("User.NotFound", "User not found.");

			if (!SubscriptionTierExtensions.TryParseTier(tier, out var parsedTier))
				return Error.Validation("Invalid.Tier", "Tier must be one of: Free, Premium.");

			var oldTierValue = (int)user.SubscriptionTier;
			var oldTierName = user.SubscriptionTier.ToString();

			if (oldTierValue == (int)parsedTier)
				return Result<string>.Ok($"User already has tier {parsedTier}.");

			user.SubscriptionTier = parsedTier;
			await _userManager.UpdateAsync(user);
			await _userManager.UpdateSecurityStampAsync(user);

			_db.UserTierHistories.Add(new UserTierHistory
			{
				UserId = userId,
				OldTier = oldTierValue,
				NewTier = (int)parsedTier,
				ChangedByUserId = changedByUserId,
				IpAddress = ipAddress
			});
			await _db.SaveChangesAsync();

			try
			{
				var userEmail = await _userManager.GetEmailAsync(user);
				if (!string.IsNullOrEmpty(userEmail))
				{
					await _emailService.SendTierChangedEmailAsync(
						userEmail,
						user.DisplayName,
						oldTierName,
						parsedTier.ToString(),
						DateTime.UtcNow
					);
				}
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Failed to send tier change email to user {UserId}", userId);
			}

			return Result<string>.Ok($"User tier set to {parsedTier}");
		}
	}
}
