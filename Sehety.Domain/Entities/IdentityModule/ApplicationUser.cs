using Microsoft.AspNetCore.Identity;
using S2S.Domain.Entities.Enums;
namespace S2S.Domain.Entities.IdentityModule
{
	public class ApplicationUser : IdentityUser
	{
		public string DisplayName { get; set; } = default!;
		public DateOnly? DateOfBirth { get; set; }
		public UserType UserType { get; set; }
		public SignLanguage SignLanguage { get; set; } = SignLanguage.Egyptian;
		public bool UsesSignLanguage {  get; set; }
		public string? ProfileImageUrl { get; set; }
		public bool IsActive { get; set; } = true;
		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public bool IsFirstLogin { get; set; } = true;
        public Address? Address { get; set; }
        public string? RefreshToken { get; set; }
        public DateTime? RefreshTokenExpiryTime { get; set; }
        public ICollection<UserOtp> Otps { get; set; } = new List<UserOtp>();

	}
}
