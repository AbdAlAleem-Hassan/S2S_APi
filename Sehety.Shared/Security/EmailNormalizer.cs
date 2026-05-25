namespace S2S.Shared.Security
{
    /// <summary>
    /// Normalizes email addresses to prevent duplicate account abuse via
    /// Gmail-specific tricks (dot insertion) and plus-addressing (all providers).
    /// 
    /// Plus-addressing (user+tag@domain) is blocked outright.
    /// Dot normalization is provider-aware (Gmail only).
    /// 
    /// The normalized form is used ONLY for duplicate detection — the original
    /// email is always stored as-is for delivery.
    /// </summary>
    public static class EmailNormalizer
    {
        /// <summary>
        /// Domains known to ignore dots in the local part.
        /// </summary>
        private static readonly HashSet<string> GmailDomains = new(StringComparer.OrdinalIgnoreCase)
        {
            "gmail.com",
            "googlemail.com"
        };

        /// <summary>
        /// Checks if the email contains a '+' in the local part.
        /// Plus-addressing is blocked for ALL providers to prevent abuse.
        /// </summary>
        public static bool ContainsPlus(string? email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            var atIndex = email.LastIndexOf('@');
            if (atIndex <= 0)
                return false;

            return email[..atIndex].Contains('+');
        }

        /// <summary>
        /// Normalizes the email for duplicate checking. Provider-aware for dots:
        /// 
        /// Gmail/Googlemail:
        ///   1. Lowercase + trim
        ///   2. Remove dots from local part
        ///   3. Strip everything after '+' in local part
        ///   Example: "U.ser+tag@Gmail.com" → "user@gmail.com"
        /// 
        /// All other providers:
        ///   1. Lowercase + trim
        ///   2. Strip everything after '+' in local part
        ///   3. Dots are kept (they are significant on non-Gmail)
        ///   Example: "John.Smith+promo@Outlook.com" → "john.smith@outlook.com"
        /// </summary>
        public static string NormalizeForDuplicateCheck(string? email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return string.Empty;

            email = email.Trim().ToLowerInvariant();

            var atIndex = email.LastIndexOf('@');
            if (atIndex <= 0 || atIndex == email.Length - 1)
                return email;

            var localPart = email[..atIndex];
            var domain = email[(atIndex + 1)..];

            // Strip '+' sub-addressing for ALL providers
            var plusIndex = localPart.IndexOf('+');
            if (plusIndex >= 0)
            {
                localPart = localPart[..plusIndex];
            }

            // Remove dots only for Gmail/Googlemail
            if (GmailDomains.Contains(domain))
            {
                localPart = localPart.Replace(".", "");
            }

            return $"{localPart}@{domain}";
        }
    }
}
