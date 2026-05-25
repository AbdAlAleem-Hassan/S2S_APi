using AutoMapper;
using FirebaseAdmin.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using S2S.Domain.Entities.IdentityModule;
using S2S.Persistence.IdentityData.DbContexts;
using S2S.ServicesAbstraction;
using S2S.Shared.CommonResult;
using S2S.Shared.Constants;
using S2S.Shared.DataTransferObjects.V1.FirebaseDTOs;
using S2S.Shared.DataTransferObjects.V1.IdentityDTOs;

namespace S2S.Services
{
    public class AuthService : IAuthService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IConfiguration _configuration;
        private readonly IEmailService _emailService;
        private readonly S2SIdentityDbContext _context;
        private readonly IMapper _mapper;
        private readonly ITokenService _tokenService;
        private readonly ILogger<AuthService> _logger;

        public AuthService(
            UserManager<ApplicationUser> userManager,
            IConfiguration configuration,
            IEmailService emailService,
            S2SIdentityDbContext context,
            IMapper mapper,
            ITokenService tokenService,
            ILogger<AuthService> logger)
        {
            _userManager = userManager;
            _configuration = configuration;
            _emailService = emailService;
            _context = context;
            _mapper = mapper;
            _tokenService = tokenService;
            _logger = logger;
        }

        public async Task<Result<UserDTO>> LoginAsync(LoginDTO loginDTO)
        {
            var user = await _userManager.FindByEmailAsync(loginDTO.Email);
            if (user is null)
            {
                _logger.LogWarning("Login failed: User not found.");
                return Error.InvalidCredentails("User.InvalidCredentials");
            }

            if (await _userManager.IsLockedOutAsync(user))
            {
                var remainingTime = Math.Ceiling((user.LockoutEnd!.Value - DateTimeOffset.UtcNow).TotalMinutes);
                _logger.LogWarning("Login failed: Account locked. UserId: {UserId}", user.Id);
                return Error.Unauthorized("AccountLocked", $"Account is locked. Try again in {remainingTime} minutes.");
            }

            if (!user.EmailConfirmed)
            {
                _logger.LogWarning("Login failed: Email not confirmed. UserId: {UserId}", user.Id);
                return Error.Unauthorized("EmailNotConfirmed", "Please verify your email first.");
            }

            var isPasswordValid = await _userManager.CheckPasswordAsync(user, loginDTO.Password);
            if (!isPasswordValid)
            {
                await _userManager.AccessFailedAsync(user);
                _logger.LogWarning("Login failed: Invalid password. UserId: {UserId}", user.Id);
                return Error.InvalidCredentails("User.InvalidCredentials");
            }

            await _userManager.ResetAccessFailedCountAsync(user);

            var token = await _tokenService.CreateAccessTokenAsync(user);
            var rawRefreshToken = AuthHelpers.GenerateRefreshToken();
            user.RefreshToken = AuthHelpers.HashRefreshToken(rawRefreshToken);
            user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(int.Parse(_configuration["JWTOptions:RefreshTokenExpiryInDays"] ?? AuthDefaults.RefreshTokenExpiryDays.ToString()));
            user.LastLoginAt = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);
            _logger.LogInformation("Login successful. UserId: {UserId}", user.Id);
            return await _tokenService.MapToUserDTOAsync(user, rawRefreshToken);
        }

        public async Task<Result> RegisterAsync(RegisterDTO registerDTO)
        {
            var normalizedEmail = registerDTO.Email.Trim().ToLowerInvariant();

            // Block plus-addressing for all providers (e.g. user+tag@any.com)
            if (S2S.Shared.Security.EmailNormalizer.ContainsPlus(normalizedEmail))
            {
                _logger.LogWarning("Registration failed: Email contains '+' addressing.");
                return Error.Validation("Email.PlusNotAllowed", "Email addresses with '+' are not allowed.");
            }

            if (registerDTO.UsesSignLanguage && !registerDTO.SignLanguage.HasValue)
            {
                _logger.LogWarning("Registration failed: Sign language required but not provided.");
                return Error.Validation("SignLanguage.Required", "Sign language is required when UsesSignLanguage is true");
            }

            if (await _userManager.Users.AnyAsync(u => u.PhoneNumber == registerDTO.PhoneNumber))
            {
                _logger.LogWarning("Registration failed: Phone number already in use.");
                return Error.Validation("DuplicatePhoneNumber", "Phone number is already in use.");
            }

            // Standard duplicate check (exact email match)
            if (await _userManager.Users.AnyAsync(u => u.NormalizedEmail == normalizedEmail.ToUpperInvariant()))
            {
                _logger.LogWarning("Registration failed: Email already in use.");
                return Error.Validation("DuplicateEmail", "Email is already in use.");
            }

            // Normalized duplicate check (Gmail dot trick / plus alias detection)
            // e.g. "u.ser+tag@gmail.com" matches existing "user@gmail.com"
            // Non-Gmail: only catches exact case-insensitive duplicates (no false positives)
            var canonicalEmail = S2S.Shared.Security.EmailNormalizer.NormalizeForDuplicateCheck(normalizedEmail);
            var allEmails = await _userManager.Users
                .Select(u => u.Email)
                .ToListAsync();

            if (allEmails.Any(e => e != null &&
                S2S.Shared.Security.EmailNormalizer.NormalizeForDuplicateCheck(e) == canonicalEmail))
            {
                _logger.LogWarning("Registration failed: Normalized email already in use. Canonical: {CanonicalEmail}", canonicalEmail);
                return Error.Validation("DuplicateEmail", "Email is already in use.");
            }

            var user = _mapper.Map<ApplicationUser>(registerDTO);

            var identityResult = await _userManager.CreateAsync(user, registerDTO.Password);

            if (identityResult.Succeeded)
            {
                var passwordHasher = new Microsoft.AspNetCore.Identity.PasswordHasher<ApplicationUser>();
                var initialHash = passwordHasher.HashPassword(user, registerDTO.Password);
                _context.UserPasswordHistories.Add(new UserPasswordHistory
                {
                    UserId = user.Id,
                    PasswordHash = initialHash,
                    CreatedAt = DateTime.UtcNow
                });

                var otpCode = AuthHelpers.GenerateOtp();
                var otpRecord = new UserOtp
                {
                    UserId = user.Id,
                    OtpHash = AuthHelpers.HashOtp(otpCode),
                    ExpiryTime = DateTime.UtcNow.AddMinutes(AuthDefaults.OtpExpiryMinutes),
                    Attempts = 0,
                    IsUsed = false
                };

                _context.UserOtps.Add(otpRecord);
                await _context.SaveChangesAsync();

                try
                {
                    await _emailService.SendOtpEmailAsync(user.Email!, otpCode);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "User created but failed to send OTP Email. UserId: {UserId}", user.Id);
                }

                _logger.LogInformation("User registered successfully. UserId: {UserId}", user.Id);
                return Result.Ok();
            }
            var errorCodes = string.Join(", ", identityResult.Errors.Select(e => e.Code));
            _logger.LogWarning("Registration failed: Identity creation errors: {ErrorCodes}", errorCodes);
            return identityResult.Errors.Select(e => Error.Validation(e.Code, e.Description)).ToList();
        }

        public async Task<Result<UserDTO>> LoginWithFirebaseAsync(FirebaseLoginDTO firebaseLoginDTO)
        {
            _logger.LogInformation("Processing Firebase Admin SDK login request.");

            try
            {
                if (FirebaseAuth.DefaultInstance == null)
                {
                    _logger.LogError("Firebase Auth is not initialized.");
                    return Error.Failure("FirebaseNotInitialized", "Firebase is not configured on the server.");
                }

                FirebaseToken decodedToken = await FirebaseAuth.DefaultInstance.VerifyIdTokenAsync(firebaseLoginDTO.IdToken);

                var firebaseUid = decodedToken.Uid;
                decodedToken.Claims.TryGetValue("email", out var emailObj);
                decodedToken.Claims.TryGetValue("name", out var nameObj);

                var email = emailObj?.ToString();
                var displayName = nameObj?.ToString();

                if (string.IsNullOrEmpty(email))
                {
                    return Error.Unauthorized("InvalidFirebaseToken", "Firebase token missing email.");
                }

                // Block plus-addressing for all providers
                if (S2S.Shared.Security.EmailNormalizer.ContainsPlus(email))
                {
                    return Error.Validation("Email.PlusNotAllowed", "Email addresses with '+' are not allowed.");
                }

                var user = await _userManager.FindByEmailAsync(email);

                if (user == null)
                {
                    // Normalized duplicate check before creating new account
                    // Catches Gmail dot/plus aliases pointing to same mailbox
                    var canonicalEmail = S2S.Shared.Security.EmailNormalizer.NormalizeForDuplicateCheck(email);
                    var allEmails = await _userManager.Users
                        .Select(u => u.Email)
                        .ToListAsync();

                    if (allEmails.Any(e => e != null &&
                        S2S.Shared.Security.EmailNormalizer.NormalizeForDuplicateCheck(e) == canonicalEmail))
                    {
                        _logger.LogWarning("Firebase login blocked: Normalized email already in use. Email: {Email}", email);
                        return Error.Validation("DuplicateEmail", "An account with this email already exists. Please login with your existing account.");
                    }

                    _logger.LogInformation("New user registering via Firebase Admin. Email: {Email}", email);

                    user = new ApplicationUser
                    {
                        Email = email,
                        UserName = email.Split('@')[0] + Guid.NewGuid().ToString()[..4],
                        EmailConfirmed = true,
                        DisplayName = displayName ?? email.Split('@')[0],
                    };

                    var createResult = await _userManager.CreateAsync(user);
                    if (!createResult.Succeeded)
                    {
                        return Error.Failure("UserCreationFailed", "Failed to create user account.");
                    }

                    await _userManager.AddLoginAsync(user, new UserLoginInfo("Firebase", firebaseUid, "Google via Firebase"));
                }
                else if (await _userManager.IsLockedOutAsync(user))
                {
                    return Error.Unauthorized("AccountLocked", "Your account is temporarily locked.");
                }

                return await ProcessLoginAsync(user);
            }
            catch (FirebaseAuthException ex)
            {
                _logger.LogWarning(ex, "Firebase token verification failed.");
                return Error.Unauthorized("InvalidFirebaseToken", "The provided Firebase token is invalid or expired.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception during Firebase login.");
                return Error.Failure("FirebaseLoginError", "An unexpected error occurred during Firebase login.");
            }
        }

        public async Task<Result> ForgotPasswordAsync(ForgotPasswordDTO forgotPasswordDTO)
        {
            _logger.LogInformation("Processing Forgot Password request.");

            var user = await _userManager.FindByEmailAsync(forgotPasswordDTO.Email);

            if (user == null)
            {
                _logger.LogWarning("Forgot Password failed: User not found.");
                return Result.Ok();
            }

            if (!user.EmailConfirmed)
            {
                _logger.LogWarning("Forgot Password denied: Email not confirmed. UserId: {UserId}", user.Id);
                return Result.Ok();
            }

            var oldOtps = await _context.UserOtps
                .Where(o => o.UserId == user.Id && !o.IsUsed)
                .ToListAsync();

            foreach (var old in oldOtps) old.IsUsed = true;

            var rawToken = AuthHelpers.GenerateSecureToken();
            var hashedToken = AuthHelpers.HashOtp(rawToken);

            var otpRecord = new UserOtp
            {
                UserId = user.Id,
                OtpHash = hashedToken,
                ExpiryTime = DateTime.UtcNow.AddMinutes(AuthDefaults.ResetTokenExpiryMinutes),
                Attempts = 0,
                IsUsed = false
            };

            _context.UserOtps.Add(otpRecord);
            await _context.SaveChangesAsync();

            var baseUrl = _configuration["AppUrls:ClientUrl"] ?? "https://yoursite.com";
            var resetLink = $"{baseUrl}/reset-password?token={rawToken}";

            try
            {
                await _emailService.SendForgotPasswordEmailAsync(user.Email!, resetLink);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send Forgot Password email. UserId: {UserId}", user.Id);
            }

            _logger.LogInformation("Forgot Password email initiated. UserId: {UserId}", user.Id);
            return Result.Ok();
        }

        public async Task<Result> ResetPasswordAsync(ResetPasswordDTO resetPasswordDTO)
        {
            _logger.LogInformation("Processing Reset Password request.");

            var hashedToken = AuthHelpers.HashOtp(resetPasswordDTO.Token);

            var tokenRecord = await _context.UserOtps
                .Include(o => o.User)
                .Where(o => o.OtpHash == hashedToken && !o.IsUsed && o.ExpiryTime > DateTime.UtcNow)
                .FirstOrDefaultAsync();

            if (tokenRecord == null)
            {
                _logger.LogWarning("Reset Password failed: Invalid or expired token.");
                return Error.Validation("InvalidToken", "Invalid or expired reset token.");
            }

            var user = tokenRecord.User;

            if (await _userManager.IsLockedOutAsync(user))
            {
                _logger.LogWarning("Reset Password denied: Account locked. UserId: {UserId}", user.Id);
                return Error.Unauthorized("AccountLocked", $"Account is locked. Try again in {Math.Ceiling((user.LockoutEnd!.Value - DateTimeOffset.UtcNow).TotalMinutes)} minutes.");
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                tokenRecord.IsUsed = true;

                var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
                var identityResult = await _userManager.ResetPasswordAsync(user, resetToken, resetPasswordDTO.NewPassword);

                if (!identityResult.Succeeded)
                {
                    await transaction.RollbackAsync();
                    var errorCodes = string.Join(", ", identityResult.Errors.Select(e => e.Code));
                    _logger.LogError("Reset Password failed: Identity errors. UserId: {UserId}, Errors: {Errors}", user.Id, errorCodes);
                    return identityResult.Errors.Select(e => Error.Validation(e.Code, e.Description)).ToList();
                }

                var passwordHasher = new Microsoft.AspNetCore.Identity.PasswordHasher<ApplicationUser>();
                var newHashForHistory = passwordHasher.HashPassword(user, resetPasswordDTO.NewPassword);
                _context.UserPasswordHistories.Add(new UserPasswordHistory
                {
                    UserId = user.Id,
                    PasswordHash = newHashForHistory,
                    CreatedAt = DateTime.UtcNow
                });

                var otherTokens = await _context.UserOtps
                    .Where(o => o.UserId == user.Id && !o.IsUsed)
                    .ToListAsync();
                foreach (var t in otherTokens) t.IsUsed = true;

                await _userManager.UpdateSecurityStampAsync(user);
                await _userManager.ResetAccessFailedCountAsync(user);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                try
                {
                    await _emailService.SendPasswordChangedEmailAsync(user.Email!, user.DisplayName ?? user.UserName!);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send password changed notification email. UserId: {UserId}", user.Id);
                }

                _logger.LogInformation("Password reset successful. UserId: {UserId}", user.Id);
                return Result.Ok();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Exception occurred during Password Reset transaction. UserId: {UserId}", user.Id);
                throw;
            }
        }

        private async Task<Result<UserDTO>> ProcessLoginAsync(ApplicationUser user)
        {
            var token = await _tokenService.CreateAccessTokenAsync(user);

            var rawRefreshToken = AuthHelpers.GenerateRefreshToken();
            user.RefreshToken = AuthHelpers.HashRefreshToken(rawRefreshToken);
            if (!int.TryParse(_configuration["JWTOptions:RefreshTokenExpiryInDays"], out int expiryDays))
                expiryDays = 7;

            user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(expiryDays);
            user.LastLoginAt = DateTime.UtcNow;

            await _userManager.UpdateAsync(user);

            _logger.LogInformation("Login successful. UserId: {UserId}", user.Id);

            return await _tokenService.MapToUserDTOAsync(user, rawRefreshToken);
        }
    }
}
