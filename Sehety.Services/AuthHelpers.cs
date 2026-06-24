using System.Security.Cryptography;
using System.Text;

namespace S2S.Services
{
    public static class AuthHelpers
    {
        public static string GenerateOtp() =>
            RandomNumberGenerator.GetInt32(100000, 999999).ToString();

        public static string HashOtp(string otp)
        {
            return BCrypt.Net.BCrypt.HashPassword(otp, workFactor: 10);
        }

        public static bool VerifyOtp(string otp, string hash)
        {
            return BCrypt.Net.BCrypt.Verify(otp, hash);
        }

        public static string HashRefreshToken(string token)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            return Convert.ToBase64String(bytes);
        }

        public static string HashSecureToken(string token)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            return Convert.ToBase64String(bytes);
        }

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
