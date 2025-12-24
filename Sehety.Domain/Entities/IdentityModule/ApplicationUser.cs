using Microsoft.AspNetCore.Identity;
namespace S2S.Domain.Entities.IdentityModule
{
	public class ApplicationUser : IdentityUser
	{
		public string DisplayName { get; set; } = default!;
		public Address? Address { get; set; }

	}
}
