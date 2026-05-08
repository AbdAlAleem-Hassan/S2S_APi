using Microsoft.AspNetCore.Http;

namespace S2S.Shared.Helpers
{
    /// <summary>
    /// Shared URL-building helper for media endpoints.
    /// Eliminates duplicated URL rewriting logic across controllers.
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

            var request = context.Request;
            var baseUrl = $"{request.Scheme}://{request.Host}{request.PathBase}";
            return $"{baseUrl}/api/v1/media/{type}/{fileName}";
        }
    }
}
