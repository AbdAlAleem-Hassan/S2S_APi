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
					await _roleManager.CreateAsync(new IdentityRole("Doctor"));
					await _roleManager.CreateAsync(new IdentityRole("Patient"));
				}

				if (!_userManager.Users.Any())
				{
					var User01 = new ApplicationUser
					{
						DisplayName = "Aleem Hassan",
						UserName = "AbdalaleemElsayed",
						Email = "Aleem@gmail.com",
						PhoneNumber = "01277277089"
					};
					var User02 = new ApplicationUser
					{
						DisplayName = "Hedra Nabil",
						UserName = "HedraNabil",
						Email = "Hedra@gmail.com",
						PhoneNumber = "01088755432"
					};

					await _userManager.CreateAsync(User01, "Pa$$w0rd");
					await _userManager.CreateAsync(User02, "Pa$$w0rd");

					await _userManager.AddToRoleAsync(User01, "Admin");
					await _userManager.AddToRoleAsync(User02, "Doctor");
				}
			}
			catch (Exception ex)
			{
				_logger.LogError($"An error occurred while seeding identity data : Message = {ex.Message}");
			}
		}
	}
}
