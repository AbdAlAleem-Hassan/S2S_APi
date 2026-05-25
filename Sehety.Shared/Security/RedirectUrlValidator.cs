using System.Text.RegularExpressions;

namespace S2S.Shared.Security
{
    /// <summary>
    /// Validates redirect URLs against a strict whitelist.
    /// Prevents Open Redirect attacks (OWASP A10) by ensuring redirects
    /// only go to trusted origins and safe paths.
    /// 
    /// Defense layers:
    /// 1. Null/empty rejection
    /// 2. Control character rejection
    /// 3. URL-encoding bypass rejection (double-encoded chars)
    /// 4. Protocol-relative URL rejection (//evil.com)
    /// 5. Dangerous scheme rejection (javascript:, data:, vbscript:)
    /// 6. Backslash bypass rejection (\/evil.com)
    /// 7. Relative paths: MUST start with /api/ (our API prefix only)
    /// 8. Absolute URLs: MUST match whitelisted origin exactly
    /// 9. All other formats: rejected
    /// </summary>
    public static class RedirectUrlValidator
    {
        /// <summary>
        /// Whitelist of allowed origins for redirect URLs.
        /// Only these domains are considered safe redirect targets.
        /// </summary>
        private static readonly HashSet<string> AllowedOrigins = new(StringComparer.OrdinalIgnoreCase)
        {
            "https://s2sai.online",
            "https://www.s2sai.online",
            "http://localhost:3000",
            "http://localhost:5173"
        };

        /// <summary>
        /// Only allow characters that are valid in URL paths + query strings.
        /// Blocks control characters, null bytes, newlines, etc.
        /// </summary>
        private static readonly Regex SafeUrlPattern = new(
            @"^[a-zA-Z0-9\-._~:/?#\[\]@!$&'()*+,;=%]+$",
            RegexOptions.Compiled);

        /// <summary>
        /// Validates that a redirect URL is safe.
        /// Returns the validated URL if safe; null if rejected.
        /// </summary>
        public static string? Validate(string? url)
        {
            // 1. Null/empty
            if (string.IsNullOrWhiteSpace(url))
                return null;

            var trimmed = url.Trim();

            // 2. Length limit (prevent abuse with very long URLs)
            if (trimmed.Length > 2048)
                return null;

            // 3. Control characters (null bytes, newlines, tabs)
            if (trimmed.Any(c => char.IsControl(c)))
                return null;

            // 4. Only allow safe URL characters
            if (!SafeUrlPattern.IsMatch(trimmed))
                return null;

            // 5. Block URL-encoding bypass attempts (%2F%2F = //, %5C = \)
            var decoded = Uri.UnescapeDataString(trimmed);
            if (decoded != trimmed)
            {
                // Re-validate the decoded version to catch encoded attacks
                if (decoded.StartsWith("//", StringComparison.Ordinal) ||
                    decoded.Contains('\\') ||
                    decoded.Contains('\0') ||
                    decoded.Any(c => char.IsControl(c)))
                    return null;
            }

            // 6. Block protocol-relative URLs (//evil.com)
            if (trimmed.StartsWith("//", StringComparison.Ordinal))
                return null;

            // 7. Block dangerous schemes
            if (trimmed.Contains(':', StringComparison.Ordinal))
            {
                var colonIndex = trimmed.IndexOf(':');
                var beforeColon = trimmed[..colonIndex].Trim().ToLowerInvariant();
                if (beforeColon is "javascript" or "data" or "vbscript" or "mailto" or "file" or "ftp")
                    return null;
            }

            // 8. Block backslash tricks (\/evil.com, \evil.com)
            if (trimmed.Contains('\\'))
                return null;

            // 9. Relative paths — ONLY allow /api/ prefix (our API paths)
            if (trimmed.StartsWith("/", StringComparison.Ordinal))
            {
                // Must be an API path — not arbitrary relative URLs
                if (!trimmed.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
                    return null;

                // Double-check: no protocol-relative after decoding
                if (decoded.StartsWith("//", StringComparison.Ordinal))
                    return null;

                return trimmed;
            }

            // 10. Absolute URLs — must match whitelisted origin EXACTLY
            if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
            {
                // Only allow http/https schemes
                if (uri.Scheme != "http" && uri.Scheme != "https")
                    return null;

                var origin = $"{uri.Scheme}://{uri.Host}";
                if (uri.Port != 80 && uri.Port != 443)
                    origin = $"{uri.Scheme}://{uri.Host}:{uri.Port}";

                if (AllowedOrigins.Contains(origin))
                    return trimmed;

                return null;
            }

            // 11. Anything else — reject
            return null;
        }

        /// <summary>
        /// Checks if a given URL is safe without returning the URL.
        /// </summary>
        public static bool IsSafe(string? url) => Validate(url) != null;
    }
}
