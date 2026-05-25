using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace S2S.Shared.Helpers
{
    /// <summary>
    /// Shared URL-building helper for media endpoints.
    /// Eliminates duplicated URL rewriting logic across controllers.
    /// 
    /// Uses configured ApiBaseUrl when available to ensure consistent URLs
    /// regardless of which domain the request arrives through
    /// (e.g., Tailscale vs production domain).
    /// </summary>
    public static class UrlRewriter
    {
        /// <summary>
        /// Builds a media URL from a file name and media type.
        /// If the fileName is already an absolute URL, it is returned unchanged.
        /// </summary>
        /// <param name="context">Current HttpContext for building the base URL.</param>
        /// <param name="fileName">File name (or absolute URL).</param>
        /// <param name="type">Media type folder (e.g. "audio", "pose", "profile", "video").</param>
        /// <returns>A fully qualified media URL, or null if fileName is empty.</returns>
        public static string? BuildMediaUrl(HttpContext context, string? fileName, string type)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return null;

            // Already an absolute URL — return as-is
            if (Uri.TryCreate(fileName, UriKind.Absolute, out _))
                return fileName;

            // Use configured API base URL for consistent URLs across all domains.
            // Falls back to request Host if not configured.
            var config = context.RequestServices.GetService<IConfiguration>();
            var configuredBaseUrl = config?["AppUrls:ApiBaseUrl"];

            string baseUrl;
            if (!string.IsNullOrWhiteSpace(configuredBaseUrl))
            {
                baseUrl = configuredBaseUrl.TrimEnd('/');
            }
            else
            {
                var request = context.Request;
                baseUrl = $"{request.Scheme}://{request.Host}{request.PathBase}";
            }

            return $"{baseUrl}/api/v1/media/{type}/{fileName}";
        }
    }
}
