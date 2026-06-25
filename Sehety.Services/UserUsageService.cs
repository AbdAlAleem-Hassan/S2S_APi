using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using S2S.Domain.Entities.Enums;
using S2S.Domain.Entities.Usage;
using S2S.Persistence.IdentityData.DbContexts;
using S2S.Shared.DataTransferObjects.V1.TranslationDTOs;

namespace S2S.Services
{
    public class UserUsageService
    {
        private readonly S2SIdentityDbContext _db;
        private readonly IConfiguration _configuration;

        public UserUsageService(S2SIdentityDbContext db, IConfiguration configuration)
        {
            _db = db;
            _configuration = configuration;
        }

        public int GetLimit(SubscriptionTier? subscriptionTier)
        {
            var limits = _configuration.GetSection("QuotaLimits:Translation");
            var tierName = (subscriptionTier ?? SubscriptionTier.Free).ToString();
            return limits.GetValue<int?>(tierName) ?? limits.GetValue("Default", 10);
        }

        public async Task<bool> TryConsumeAsync(string userId, UsageType quotaType, SubscriptionTier? subscriptionTier)
        {
            var now = DateTime.UtcNow;
            var windowStart = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, DateTimeKind.Utc);
            var limit = GetLimit(subscriptionTier);

            if (limit < 0) return true;

            var total = await _db.UserUsages
                .Where(u => u.UserId == userId && u.WindowStart == windowStart)
                .SumAsync(u => (int?)u.Count) ?? 0;

            if (total >= limit) return false;

            try
            {
                _db.UserUsages.Add(new UserUsage
                {
                    UserId = userId,
                    WindowStart = windowStart,
                    Count = 1,
                    QuotaType = quotaType
                });
                await _db.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateException)
            {
                var affected = await _db.UserUsages
                    .Where(u => u.UserId == userId && u.WindowStart == windowStart && u.Count < limit)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(u => u.Count, u => u.Count + 1)
                        .SetProperty(u => u.QuotaType, quotaType));

                return affected > 0;
            }
        }

        public async Task<UserUsageInfo> GetUsageAsync(string userId, SubscriptionTier? subscriptionTier)
        {
            var now = DateTime.UtcNow;
            var windowStart = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, DateTimeKind.Utc);
            var limit = GetLimit(subscriptionTier);

            var totalCount = await _db.UserUsages
                .Where(u => u.UserId == userId && u.WindowStart == windowStart)
                .SumAsync(u => (int?)u.Count) ?? 0;

            return new UserUsageInfo
            {
                Used = totalCount,
                Limit = limit >= 0 ? limit : 0,
                IsUnlimited = limit < 0,
                ResetsAt = windowStart.AddHours(1),
                Tier = (subscriptionTier ?? SubscriptionTier.Free).ToString()
            };
        }
    }
}
