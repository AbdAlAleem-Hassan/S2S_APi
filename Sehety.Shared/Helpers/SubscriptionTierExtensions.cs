using S2S.Domain.Entities.Enums;

namespace S2S.Shared.Helpers
{
    public static class SubscriptionTierExtensions
    {
        private static readonly HashSet<string> ValidValues = new(StringComparer.OrdinalIgnoreCase)
        {
            "Free",
            "Premium"
        };

        public static bool TryParseTier(string? value, out SubscriptionTier tier)
        {
            tier = SubscriptionTier.Free;

            if (string.IsNullOrWhiteSpace(value))
                return false;

            value = value.Trim();

            if (!ValidValues.Contains(value))
                return false;

            if (value.Equals("Premium", StringComparison.OrdinalIgnoreCase))
            {
                tier = SubscriptionTier.Premium;
                return true;
            }

            tier = SubscriptionTier.Free;
            return true;
        }
    }
}
