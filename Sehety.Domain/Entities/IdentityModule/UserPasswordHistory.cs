namespace S2S.Domain.Entities.IdentityModule
{
    public class UserPasswordHistory
    {
        public int Id { get; set; }
        public string UserId { get; set; } = default!;
        public string PasswordHash { get; set; } = default!;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        
        public ApplicationUser User { get; set; } = default!;
    }
}
