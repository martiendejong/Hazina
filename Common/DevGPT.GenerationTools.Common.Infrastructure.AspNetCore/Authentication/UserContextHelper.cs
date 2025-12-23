using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Common.Infrastructure.AspNetCore.Authentication
{
    /// <summary>
    /// Helper class for extracting the userId from HttpContext in a unified and robust manner.
    ///
    /// Order:
    ///    1. Try Claims: Looks in both "sub" (standard OpenID Connect subject identifier) and ClaimTypes.NameIdentifier.
    ///    2. If not found or empty, tries the "userId" HTTP header (handles scenarios for anonymous/third-party call-backs).
    ///    3. Returns null if not found.
    ///
    /// Should be used in all endpoints, including those marked [AllowAnonymous].
    /// Handles unauthenticated/anonymous calls gracefully by returning null if no userId is available.
    /// </summary>
    public static class UserContextHelper
    {
        /// <summary>
        /// Extracts the user ID from the HttpContext, checking claims first, then headers.
        /// </summary>
        /// <param name="ctx">The current HttpContext</param>
        /// <returns>The user ID if found, otherwise null</returns>
        public static string? GetUserId(HttpContext? ctx)
        {
            if (ctx == null) return null;

            // Try to obtain from claims: "sub" (OpenID) or ClaimTypes.NameIdentifier.
            var user = ctx.User;
            string? userId = null;
            if (user != null && user.Identity != null && user.Identity.IsAuthenticated)
            {
                userId = user.FindFirst("sub")?.Value;
                if (string.IsNullOrWhiteSpace(userId))
                    userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            }

            // If not found, check the header for fallback (covers AllowAnonymous & direct API callback scenarios).
            if (string.IsNullOrWhiteSpace(userId))
            {
                if (ctx.Request.Headers.TryGetValue("userId", out var headerValue) && !string.IsNullOrWhiteSpace(headerValue))
                {
                    userId = headerValue.ToString();
                }
            }

            // Returns null if neither method worked.
            return string.IsNullOrWhiteSpace(userId) ? null : userId;
        }
    }
}
