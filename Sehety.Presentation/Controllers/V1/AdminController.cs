using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using S2S.ServicesAbstraction;
using S2S.Shared.DataTransferObjects.V1.AdminDTOs;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace S2S.Presentation.Controllers.V1
{
	[Authorize(Roles = "Admin")]
	[ApiVersion("1.0")]
	[Route("api/v{version:apiVersion}/Admin")]
	[EnableRateLimiting("auth-limit")]
	public class AdminController(IAdminService _adminService) : ApiBaseController
	{
		[HttpGet("users")]
		[EndpointSummary("Get All Users")]
		[EndpointDescription("Retrieve a list of all users excluding the currently logged-in admin.")]
		public async Task<ActionResult<IEnumerable<DashUserDto>>> GetUsers()
		{
			var currentUserId = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
			var result = await _adminService.GetAllUsersAsync(currentUserId);

			if (result.IsSuccess)
			{
				return Ok(result);
			}
			return HandleRequest(result);
		}

		[HttpPut("users/{id}/toggle-lock")]
		[EndpointSummary("Lock or Unlock User")]
		[EndpointDescription("Toggles the lock status of a user by their ID.")]
		public async Task<ActionResult<string>> ToggleLockStatus(string id)
		{
			var currentUserId = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
			if (string.Equals(id, currentUserId, StringComparison.OrdinalIgnoreCase))
			{
				return BadRequest(new { error = "You cannot lock your own account." });
			}

			var result = await _adminService.ToggleUserLockStatusAsync(id);

			if (result.IsSuccess)
			{
				return Ok(result);
			}
			return HandleRequest(result);
		}

		[HttpPut("users/{id}/tier")]
		[EndpointSummary("Set User Subscription Tier")]
		[EndpointDescription("Set user tier. Valid values: Free (10 requests/hr), Premium (unlimited).")]
		public async Task<ActionResult<string>> SetUserTier(string id, [FromBody] SetTierRequest request)
		{
			var adminId = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
			var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

			var result = await _adminService.SetUserTierAsync(id, request.Tier, adminId!, ipAddress);

			if (result.IsSuccess)
			{
				return Ok(result);
			}
			return HandleRequest(result);
		}
	}
}
