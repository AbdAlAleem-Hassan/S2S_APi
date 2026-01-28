using System;

namespace S2S.Domain.Entities.IdentityModule
{
    public class UserOtp
    {
        public int Id { get; set; }
        public string UserId { get; set; } = default!;
        public string OtpHash { get; set; } = default!;
        public DateTime ExpiryTime { get; set; }
        public int Attempts { get; set; } = 0;
        public bool IsUsed { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation Property
        public ApplicationUser User { get; set; } = default!;
    }
}
