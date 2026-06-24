namespace S2S.Domain.Entities.Usage
{
    public class UserTierHistory
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string UserId { get; set; } = default!;
        public int OldTier { get; set; }
        public int NewTier { get; set; }
        public string? ChangedByUserId { get; set; }
        public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
        public string? IpAddress { get; set; }
    }
}
