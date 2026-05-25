using System.Security.Cryptography;
using System.Text;

namespace S2S.Services
{
    /// <summary>
    /// Shared static utility methods used by all auth-related services.
    /// </summary>
    public static class AuthHelpers
    {
        public static string GenerateOtp() =>
            RandomNumberGenerator.GetInt32(100000, 999999).ToString();

        public static string HashOtp(string otp)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(otp));
            return Convert.ToBase64String(bytes);
        }

        /// <summary>
        /// Hashes a refresh token before storing in the database.
        /// The plaintext token is returned to the client; only the hash is persisted.
        /// </summary>
        public static string HashRefreshToken(string token) => HashOtp(token);

        public static string GenerateSecureToken()
        {
            var bytes = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);
            return Convert.ToHexString(bytes);
        }

        public static string GenerateRefreshToken()
        {
            var randomNumber = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);
            return Convert.ToBase64String(randomNumber);
        }
    }
}
