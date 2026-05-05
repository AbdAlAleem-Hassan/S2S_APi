using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using S2S.Domain.Contracts;
using S2S.Domain.Entities.IdentityModule;
using System;
using System.Collections.Generic;
using System.Text;

namespace S2S.Persistence.IdentityData.DataSeed
{
	public class IdentityDataInitializer : IDataInitializer
	{
		private readonly UserManager<ApplicationUser> _userManager;
		private readonly RoleManager<IdentityRole> _roleManager;
		private readonly ILogger<IdentityDataInitializer> _logger;

		public IdentityDataInitializer(UserManager<ApplicationUser> userManager,
			RoleManager<IdentityRole> roleManager,
			ILogger<IdentityDataInitializer> logger)
		{
			_userManager = userManager;
			_roleManager = roleManager;
			_logger = logger;
		}
		public async Task InitializeAsync()
		{
			try
			{
				if (!_roleManager.Roles.Any())
				{
					await _roleManager.CreateAsync(new IdentityRole("Admin"));
					await _roleManager.CreateAsync(new IdentityRole("User"));
				}

				if (!_userManager.Users.Any())
				{
					var User01 = new ApplicationUser
					{
						DisplayName = "Aleem Hassan",
						UserName = "AbdalaleemElsayed",
						Email = "nawiya7975@kynninc.com",
						PhoneNumber = "01277277089"
					};
					

					await _userManager.CreateAsync(User01, "PaS_Admin#123");

					await _userManager.AddToRoleAsync(User01, "Admin");
				}
			}
			catch (Exception ex)
			{
				_logger.LogError($"An error occurred while seeding identity data : Message = {ex.Message}");
			}
		}
	}
}
