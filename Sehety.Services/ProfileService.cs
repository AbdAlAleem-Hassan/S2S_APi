using AutoMapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using S2S.Domain.Entities.IdentityModule;
using S2S.Persistence.IdentityData.DbContexts;
using S2S.ServicesAbstraction;
using S2S.Shared.CommonResult;
using S2S.Shared.Constants;
using S2S.Shared.DataTransferObjects.V1.IdentityDTOs;

namespace S2S.Services
{
    public class ProfileService : IProfileService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IConfiguration _configuration;
        private readonly S2SIdentityDbContext _context;
        private readonly IMapper _mapper;
        private readonly IEmailService _emailService;
        private readonly ITokenService _tokenService;
        private readonly ILogger<ProfileService> _logger;

        public ProfileService(
            UserManager<ApplicationUser> userManager,
            IConfiguration configuration,
            S2SIdentityDbContext context,
            IMapper mapper,
            IEmailService emailService,
            ITokenService tokenService,
            ILogger<ProfileService> logger)
        {
            _userManager = userManager;
            _configuration = configuration;
            _context = context;
            _mapper = mapper;
            _emailService = emailService;
            _tokenService = tokenService;
            _logger = logger;
        }

        public async Task<Result<UserDTO>> GetUserByEmailAsync(string email)
        {
            var User = await _userManager.FindByEmailAsync(email);
            if (User is null)
            {
                _logger.LogWarning("User retrieval failed: User lookup returned null.");
                return Error.NotFound("User.NotFound", $"No User With Email {{{email}}} Was Exist");
            }
            _logger.LogInformation("User retrieved successfully. UserId: {UserId}", User.Id);
            return await _tokenService.MapToUserDTOAsync(User);
        }

        public async Task<Result> UpdateFcmTokenAsync(string email, string fcmToken)
        {
            _logger.LogInformation("Processing Update FCM Token request.");

            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                _logger.LogWarning("Update FCM Token failed: User not found.");
                return Error.NotFound("UserNotFound", "User does not exist.");
            }

            user.FcmToken = fcmToken;
            var updateResult = await _userManager.UpdateAsync(user);

            if (!updateResult.Succeeded)
            {
                var errorCodes = string.Join(", ", updateResult.Errors.Select(e => e.Code));
                _logger.LogError("Update FCM Token failed. UserId: {UserId}, Errors: {Errors}", user.Id, errorCodes);
                return Error.Failure("UpdateFailed", "Failed to update FCM token.");
            }

            _logger.LogInformation("FCM Token updated successfully. UserId: {UserId}", user.Id);
            return Result.Ok();
        }

        public async Task<Result<UpdateProfileResponseDTO>> UpdateProfileAsync(
            string userId,
            UpdateProfileDTO updateProfileDTO,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Processing Update Profile request. UserId: {UserId}", userId);

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                _logger.LogWarning("Update Profile failed: User not found. UserId: {UserId}", userId);
                return Error.NotFound("UserNotFound", "User not found.");
            }

            var displayName = updateProfileDTO.DisplayName?.Trim();
            if (string.IsNullOrWhiteSpace(displayName))
            {
                return Error.Validation("DisplayName.Required", "Display name is required.");
            }

            if (!string.Equals(user.DisplayName, displayName, StringComparison.Ordinal))
            {
                user.DisplayName = displayName;
            }

            if (!string.IsNullOrWhiteSpace(updateProfileDTO.PhoneNumber))
            {
                var phoneInUse = await _userManager.Users
                    .AnyAsync(u => u.PhoneNumber == updateProfileDTO.PhoneNumber && u.Id != userId, cancellationToken);
                if (phoneInUse)
                {
                    return Error.Validation("DuplicatePhoneNumber", "Phone number is already in use.");
                }

                user.PhoneNumber = updateProfileDTO.PhoneNumber;
            }

            user.UpdatedAt = DateTime.UtcNow;
            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                var errorCodes = string.Join(", ", updateResult.Errors.Select(e => e.Code));
                _logger.LogError("Update Profile failed. UserId: {UserId}, Errors: {Errors}", user.Id, errorCodes);
                return updateResult.Errors.Select(e => Error.Validation(e.Code, e.Description)).ToList();
            }

            return new UpdateProfileResponseDTO(user.DisplayName, user.PhoneNumber, user.ProfileImageUrl);
        }

        public async Task<Result> ChangePasswordAsync(string userId, ChangePasswordDTO changePasswordDTO)
        {
            _logger.LogInformation("Processing Change Password request. UserId: {UserId}", userId);

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                _logger.LogWarning("Change Password failed: User not found. UserId: {UserId}", userId);
                return Error.NotFound("UserNotFound", "User not found.");
            }

            if (await _userManager.IsLockedOutAsync(user))
            {
                var remaining = Math.Ceiling((user.LockoutEnd!.Value - DateTimeOffset.UtcNow).TotalMinutes);
                _logger.LogWarning("Change Password denied: Account locked. UserId: {UserId}", user.Id);
                return Error.Unauthorized("AccountLocked", $"Account is locked. Try again in {remaining} minutes.");
            }

            var isCurrentPasswordValid = await _userManager.CheckPasswordAsync(user, changePasswordDTO.CurrentPassword);
            if (!isCurrentPasswordValid)
            {
                await _userManager.AccessFailedAsync(user);
                _logger.LogWarning("Change Password failed: Wrong current password. UserId: {UserId}", user.Id);
                return Error.Validation("InvalidCurrentPassword", "Current password is incorrect.");
            }

            const int PasswordHistoryLimit = 5;
            var recentPasswords = await _context.UserPasswordHistories
                .Where(p => p.UserId == user.Id)
                .OrderByDescending(p => p.CreatedAt)
                .Take(PasswordHistoryLimit)
                .ToListAsync();

            var passwordHasher = new Microsoft.AspNetCore.Identity.PasswordHasher<ApplicationUser>();
            foreach (var old in recentPasswords)
            {
                var verificationResult = passwordHasher.VerifyHashedPassword(user, old.PasswordHash, changePasswordDTO.NewPassword);
                if (verificationResult != Microsoft.AspNetCore.Identity.PasswordVerificationResult.Failed)
                {
                    _logger.LogWarning("Change Password denied: New password matches a previously used password. UserId: {UserId}", user.Id);
                    return Error.Validation("PasswordPreviouslyUsed", $"You cannot reuse any of your last {PasswordHistoryLimit} passwords.");
                }
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var identityResult = await _userManager.ChangePasswordAsync(user, changePasswordDTO.CurrentPassword, changePasswordDTO.NewPassword);
                if (!identityResult.Succeeded)
                {
                    await transaction.RollbackAsync();
                    var errorCodes = string.Join(", ", identityResult.Errors.Select(e => e.Code));
                    _logger.LogError("Change Password failed: Identity errors. UserId: {UserId}, Errors: {Errors}", user.Id, errorCodes);
                    return identityResult.Errors.Select(e => Error.Validation(e.Code, e.Description)).ToList();
                }

                var newHashForHistory = passwordHasher.HashPassword(user, changePasswordDTO.NewPassword);
                _context.UserPasswordHistories.Add(new UserPasswordHistory
                {
                    UserId = user.Id,
                    PasswordHash = newHashForHistory,
                    CreatedAt = DateTime.UtcNow
                });

                var allHistory = await _context.UserPasswordHistories
                    .Where(p => p.UserId == user.Id)
                    .OrderByDescending(p => p.CreatedAt)
                    .ToListAsync();

                var toDelete = allHistory.Skip(PasswordHistoryLimit).ToList();
                if (toDelete.Count > 0)
                    _context.UserPasswordHistories.RemoveRange(toDelete);

                user.RefreshToken = null;
                user.RefreshTokenExpiryTime = null;
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

                _logger.LogInformation("Password changed successfully. All sessions invalidated. UserId: {UserId}", user.Id);
                return Result.Ok();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Exception during Change Password transaction. UserId: {UserId}", user.Id);
                throw;
            }
        }

        public async Task<Result> RequestEmailChangeAsync(string userId, ChangeEmailDTO changeEmailDTO)
        {
            _logger.LogInformation("Processing Change Email request. UserId: {UserId}", userId);

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return Error.NotFound("UserNotFound", "User not found.");

            // 1. Verify current password (identity confirmation)
            var isPasswordValid = await _userManager.CheckPasswordAsync(user, changeEmailDTO.CurrentPassword);
            if (!isPasswordValid)
            {
                await _userManager.AccessFailedAsync(user);
                _logger.LogWarning("Change Email failed: Wrong password. UserId: {UserId}", user.Id);
                return Error.Validation("InvalidCurrentPassword", "Current password is incorrect.");
            }

            // 2. Check new email is different
            if (string.Equals(user.Email, changeEmailDTO.NewEmail, StringComparison.OrdinalIgnoreCase))
                return Error.Validation("Email.SameAsCurrent", "New email must be different from current email.");

            // 3. Check new email is not taken
            var existingUser = await _userManager.FindByEmailAsync(changeEmailDTO.NewEmail);
            if (existingUser != null)
                return Error.Validation("DuplicateEmail", "Email is already in use.");

            // 4. Cooldown: prevent spamming OTP requests (1 minute)
            var lastOtp = await _context.UserOtps
                .Where(o => o.UserId == user.Id)
                .OrderByDescending(o => o.CreatedAt)
                .FirstOrDefaultAsync();

            if (lastOtp != null && lastOtp.CreatedAt > DateTime.UtcNow.AddSeconds(-AuthDefaults.ResendOtpCooldownSeconds))
            {
                _logger.LogWarning("Change Email throttled: OTP cooldown active. UserId: {UserId}", user.Id);
                return Error.Validation("PleaseWait", "Please wait a minute before requesting a new code.");
            }

            // 5. Generate and store OTP (hashed — never stored in plaintext)
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

            // 6. Send OTP to the NEW email (not the current one)
            try
            {
                await _emailService.SendEmailChangeOtpAsync(
                    changeEmailDTO.NewEmail, otpCode, user.DisplayName ?? "User");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email change OTP. UserId: {UserId}", user.Id);
            }

            _logger.LogInformation("Email change OTP sent to new email. UserId: {UserId}", user.Id);
            return Result.Ok();
        }

        public async Task<Result> ConfirmEmailChangeAsync(string userId, ConfirmEmailChangeDTO confirmEmailChangeDTO)
        {
            _logger.LogInformation("Processing Confirm Email Change. UserId: {UserId}", userId);

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return Error.NotFound("UserNotFound", "User not found.");

            // 1. Find the latest valid OTP (not expired, not used)
            var latestOtp = await _context.UserOtps
                .Where(o => o.UserId == user.Id && !o.IsUsed && o.ExpiryTime > DateTime.UtcNow)
                .OrderByDescending(o => o.CreatedAt)
                .FirstOrDefaultAsync();

            if (latestOtp == null)
            {
                _logger.LogWarning("Confirm Email failed: No active OTP. UserId: {UserId}", user.Id);
                return Error.Validation("InvalidOtp", "No active verification code found. Please request a new one.");
            }

            // 2. Brute force protection: max attempts check
            if (latestOtp.Attempts >= AuthDefaults.MaxOtpAttempts)
            {
                _logger.LogWarning("Confirm Email failed: Max OTP attempts reached. UserId: {UserId}", user.Id);
                return Error.Validation("MaxAttemptsReached", "Maximum attempts reached. Please request a new code.");
            }

            // 3. Verify OTP hash
            if (latestOtp.OtpHash != AuthHelpers.HashOtp(confirmEmailChangeDTO.Otp))
            {
                latestOtp.Attempts++;
                var remaining = AuthDefaults.MaxOtpAttempts - latestOtp.Attempts;

                if (latestOtp.Attempts >= AuthDefaults.MaxOtpAttempts)
                {
                    latestOtp.IsUsed = true; // Invalidate OTP after max attempts
                    _logger.LogWarning("OTP invalidated: Max attempts reached. UserId: {UserId}", user.Id);
                }

                await _context.SaveChangesAsync();
                return Error.Validation("WrongOtp", $"Invalid verification code. Remaining attempts: {remaining}");
            }

            // 4. Re-check email not taken (race condition protection)
            var emailTaken = await _userManager.FindByEmailAsync(confirmEmailChangeDTO.NewEmail);
            if (emailTaken != null)
            {
                _logger.LogWarning("Confirm Email failed: Email taken (race condition). UserId: {UserId}", user.Id);
                return Error.Validation("DuplicateEmail", "Email is already in use.");
            }

            // 5. Transaction: update email + invalidate sessions atomically
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                latestOtp.IsUsed = true;

                var oldEmail = user.Email;
                user.Email = confirmEmailChangeDTO.NewEmail;
                user.NormalizedEmail = confirmEmailChangeDTO.NewEmail.ToUpperInvariant();

                // Invalidate all sessions (force re-login)
                user.RefreshToken = null;
                user.RefreshTokenExpiryTime = null;
                await _userManager.UpdateSecurityStampAsync(user);

                await _context.SaveChangesAsync();

                var identityResult = await _userManager.UpdateAsync(user);
                if (!identityResult.Succeeded)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError("Confirm Email failed: Identity update error. UserId: {UserId}", user.Id);
                    return Error.Failure("UpdateFailed", "Failed to update email.");
                }

                await transaction.CommitAsync();

                _logger.LogInformation("Email changed successfully from {OldEmail} to {NewEmail}. UserId: {UserId}",
                    oldEmail, confirmEmailChangeDTO.NewEmail, user.Id);

                // Notify both old and new email addresses
                try
                {
                    await _emailService.SendEmailChangedNotificationAsync(
                        oldEmail!, confirmEmailChangeDTO.NewEmail, user.DisplayName ?? "User");
                }
                catch (Exception emailEx)
                {
                    _logger.LogError(emailEx, "Failed to send email change notification. UserId: {UserId}", user.Id);
                }

                return Result.Ok();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Exception during Confirm Email Change transaction. UserId: {UserId}", user.Id);
                throw;
            }
        }
    }
}
