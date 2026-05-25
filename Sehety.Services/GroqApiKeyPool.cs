using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace S2S.Services
{
    /// <summary>
    /// Thread-safe round-robin API key pool with automatic quota-exhaustion handling.
    /// 
    /// - Distributes requests across multiple API keys evenly.
    /// - When a key hits quota (429), it's temporarily disabled for a cooldown period.
    /// - Keys are automatically re-enabled after the cooldown expires.
    /// - If ALL keys are exhausted, returns null so the caller can return a proper error.
    /// </summary>
    public sealed class GroqApiKeyPool
    {
        private readonly KeyEntry[] _keys;
        private int _currentIndex;
        private readonly ILogger<GroqApiKeyPool> _logger;

        /// <summary>
        /// Default cooldown when a key hits quota limit (429).
        /// Groq rate limits typically reset within 60 seconds.
        /// </summary>
        private static readonly TimeSpan QuotaCooldown = TimeSpan.FromSeconds(60);

        private sealed class KeyEntry
        {
            public string ApiKey { get; }
            public DateTime? DisabledUntil { get; set; }
            public bool IsAvailable => DisabledUntil == null || DateTime.UtcNow >= DisabledUntil;

            public KeyEntry(string apiKey)
            {
                ApiKey = apiKey;
            }
        }

        public GroqApiKeyPool(IConfiguration configuration, ILogger<GroqApiKeyPool> logger)
        {
            _logger = logger;

            // Read API keys from config: Groq:ApiKeys (array) or fallback to single Groq:ApiKey
            var apiKeys = configuration.GetSection("Groq:ApiKeys").Get<string[]>();

            if (apiKeys == null || apiKeys.Length == 0)
            {
                // Fallback: single key from Groq:ApiKey or GROQ_API_KEY env var
                var singleKey = configuration["GROQ_API_KEY"] ?? configuration["Groq:ApiKey"];
                if (!string.IsNullOrWhiteSpace(singleKey))
                {
                    apiKeys = new[] { singleKey };
                }
            }

            if (apiKeys == null || apiKeys.Length == 0)
            {
                _logger.LogError("No Groq API keys configured. Add Groq:ApiKeys array or Groq:ApiKey to configuration.");
                _keys = Array.Empty<KeyEntry>();
                return;
            }

            // Filter out empty/whitespace keys
            _keys = apiKeys
                .Where(k => !string.IsNullOrWhiteSpace(k))
                .Select(k => new KeyEntry(k.Trim()))
                .ToArray();

            _logger.LogInformation("GroqApiKeyPool initialized with {Count} API key(s).", _keys.Length);
        }

        /// <summary>
        /// Gets the next available API key using round-robin.
        /// Returns null if all keys are temporarily disabled (quota exhausted).
        /// </summary>
        public string? GetNextKey()
        {
            if (_keys.Length == 0)
                return null;

            // Try each key once (round-robin)
            for (int i = 0; i < _keys.Length; i++)
            {
                var index = Interlocked.Increment(ref _currentIndex) % _keys.Length;
                // Handle negative modulo for int overflow safety
                if (index < 0) index += _keys.Length;

                var entry = _keys[index];
                if (entry.IsAvailable)
                    return entry.ApiKey;
            }

            _logger.LogWarning("All {Count} Groq API keys are quota-exhausted. Cooldown active.", _keys.Length);
            return null;
        }

        /// <summary>
        /// Marks a key as quota-exhausted (temporarily disabled).
        /// Called when a 429 response is received.
        /// </summary>
        public void MarkQuotaExhausted(string apiKey)
        {
            var entry = _keys.FirstOrDefault(k => k.ApiKey == apiKey);
            if (entry != null)
            {
                entry.DisabledUntil = DateTime.UtcNow.Add(QuotaCooldown);
                var availableCount = _keys.Count(k => k.IsAvailable);
                _logger.LogWarning(
                    "Groq API key ending in ...{KeySuffix} marked as quota-exhausted for {Cooldown}s. {Available}/{Total} keys available.",
                    apiKey[^4..], QuotaCooldown.TotalSeconds, availableCount, _keys.Length);
            }
        }

        /// <summary>
        /// Returns the total number of configured keys.
        /// </summary>
        public int TotalKeys => _keys.Length;

        /// <summary>
        /// Returns the number of currently available keys.
        /// </summary>
        public int AvailableKeys => _keys.Count(k => k.IsAvailable);
    }
}
