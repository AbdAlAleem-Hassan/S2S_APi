using S2S.Domain.Entities.Enums;

namespace S2S.Domain.Entities.Usage
{
    public class UserUsage
    {
        public string UserId { get; set; } = default!;
        public DateTime WindowStart { get; set; }
        public int Count { get; set; }
        public UsageType QuotaType { get; set; }
    }
}
