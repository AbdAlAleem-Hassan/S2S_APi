namespace S2S.Shared.Constants
{
    public static class RateLimitPolicies
    {
        public const string AuthLimit = "auth-limit";
        public const string OtpRequestLimit = "otp-request-limit";
        public const string OtpVerifyLimit = "otp-verify-limit";
        public const string ChangePasswordLimit = "change-password-limit";
        public const string SttLimit = "stt-limit";
        public const string MediaLimit = "media-limit";
        public const string ProfileImageUploadLimit = "profile-image-upload-limit";
        public const string ResendOtpLimit = "resend-otp-limit";
        public const string TranslationQuota = "translation-quota";
    }

    public static class CookieNames
    {
        public const string RefreshToken = "refreshToken";
        public const string XsrfToken = "XSRF-TOKEN";
    }

    public static class AuthDefaults
    {
        public const int OtpLength = 6;
        public const int OtpExpiryMinutes = 10;
        public const int ResetTokenExpiryMinutes = 30;
        public const int MaxOtpAttempts = 3;
        public const int PasswordHistoryLimit = 5;
        public const int RefreshTokenExpiryDays = 7;
        public const int AccessTokenExpiryMinutes = 15;
        public const int AccountLockoutMinutes = 15;
        public const int MaxFailedAccessAttempts = 3;
        public const int PasswordMinLength = 8;
        public const int ResendOtpCooldownSeconds = 60;
        public const int UnverifiedAccountExpiryHours = 24;
    }

    public static class MediaDefaults
    {
        public const long MaxVideoSizeBytes = 50L * 1024 * 1024;
        public const long MaxAudioSizeBytes = 18L * 1024 * 1024;
        public const long MaxProfileImageSizeBytes = 5L * 1024 * 1024;
        public const int MediaRateLimitPermits = 60;
        public const int MediaRateLimitWindowMinutes = 1;
    }

    public static class CorsDefaults
    {
        public const string AllowFrontendPolicy = "AllowFrontend";
    }

    public static class ValidationDefaults
    {
        // Email
        public const int MaxEmailLength = 256;

        // Display Name
        public const int MaxDisplayNameLength = 50;

        // Username
        public const int MinUserNameLength = 3;
        public const int MaxUserNameLength = 30;

        // Password
        public const int PasswordMaxLength = 100;

        // Phone (Egyptian numbers only)
        public const string PhoneRegex = @"^01[0125]\d{8}$";
        public const string PhoneErrorMessage = "Phone number must be a valid Egyptian number (e.g. 01XXXXXXXXX).";

        // Translation Text
        public const int MaxTranslationTextLength = 200;

        // Text-to-Speech
        public const int MaxTtsTextLength = 200;
    }
}
