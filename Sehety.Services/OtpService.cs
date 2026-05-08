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
    public class OtpService : IOtpService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IConfiguration _configuration;
        private readonly S2SIdentityDbContext _context;
        private readonly IMapper _mapper;
        private readonly IEmailService _emailService;
        private readonly ITokenService _tokenService;
        private readonly ILogger<OtpService> _logger;

        public OtpService(
            UserManager<ApplicationUser> userManager,
            IConfiguration configuration,
            S2SIdentityDbContext context,
            IMapper mapper,
            IEmailService emailService,
            ITokenService tokenService,
            ILogger<OtpService> logger)
        {
            _userManager = userManager;
            _configuration = configuration;
            _context = context;
            _mapper = mapper;
            _emailService = emailService;
            _tokenService = tokenService;
            _logger = logger;
        }

        public async Task<Result<UserDTO>> VerifyOtpAsync(VerifyOtpDTO verifyOtpDTO)
        {
            var user = await _userManager.FindByEmailAsync(verifyOtpDTO.Email);
            if (user == null)
            {
                _logger.LogWarning("OTP Verification failed: User not found.");
                return Error.NotFound("UserNotFound", "User not found.");
            }

            if (user.EmailConfirmed)
            {
                _logger.LogInformation("OTP Verification skipped: User already verified. UserId: {UserId}", user.Id);
                return Error.Validation("AlreadyVerified", "Email is already verified.");
            }

            var latestOtp = await _context.UserOtps
                .Where(o => o.UserId == user.Id && !o.IsUsed && o.ExpiryTime > DateTime.UtcNow)
                .OrderByDescending(o => o.CreatedAt)
                .FirstOrDefaultAsync();

            if (latestOtp == null)
            {
                _logger.LogWarning("OTP Verification failed: No active/valid OTP found. UserId: {UserId}", user.Id);
                return Error.Validation("InvalidOtp", "No active verification code found.");
            }

            if (latestOtp.Attempts >= AuthDefaults.MaxOtpAttempts)
            {
                _logger.LogWarning("OTP Verification failed: Max attempts reached previously. UserId: {UserId}", user.Id);
                return Error.Validation("MaxAttemptsReached", "Maximum attempts reached. Please request a new code.");
            }

            if (latestOtp.OtpHash != AuthHelpers.HashOtp(verifyOtpDTO.Otp))
            {
                latestOtp.Attempts++;
                var remaining = AuthDefaults.MaxOtpAttempts - latestOtp.Attempts;
                if (latestOtp.Attempts >= AuthDefaults.MaxOtpAttempts)
                {
                    latestOtp.IsUsed = true;
                    _logger.LogWarning("OTP Invalidated: Max attempts reached during verification. UserId: {UserId}", user.Id);
                }
                else
                {
                    _logger.LogWarning("OTP Verification failed: Invalid code provided. UserId: {UserId}, Remaining: {Remaining}", user.Id, remaining);
                }
                await _context.SaveChangesAsync();
                return Error.Validation("WrongOtp", $"Invalid verification code. Remaining attempts: {AuthDefaults.MaxOtpAttempts - latestOtp.Attempts}");
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                latestOtp.IsUsed = true;
                user.EmailConfirmed = true;
                var rawRefreshToken = AuthHelpers.GenerateRefreshToken();
                user.RefreshToken = AuthHelpers.HashRefreshToken(rawRefreshToken);
                user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(int.Parse(_configuration["JWTOptions:RefreshTokenExpiryInDays"] ?? AuthDefaults.RefreshTokenExpiryDays.ToString()));

                await _context.SaveChangesAsync();
                var identityResult = await _userManager.UpdateAsync(user);

                if (!identityResult.Succeeded)
                {
                    await transaction.RollbackAsync();
                    var errorCodes = string.Join(", ", identityResult.Errors.Select(e => e.Code));
                    _logger.LogError("OTP Verification failed: User update failed. UserId: {UserId}, Errors: {Errors}", user.Id, errorCodes);
                    return Error.Failure("UpdateFailed", "Failed to update user status.");
                }

                await transaction.CommitAsync();
                _logger.LogInformation("OTP Verification successful. User verified. UserId: {UserId}", user.Id);
                return await _tokenService.MapToUserDTOAsync(user, rawRefreshToken);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Exception occurred during OTP verification transaction. UserId: {UserId}", user.Id);
                throw;
            }
        }

        public async Task<Result> ResendOtpAsync(string email)
        {
            _logger.LogInformation("Processing Resend OTP request.");

            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                _logger.LogWarning("Resend OTP failed: User not found.");
                return Error.NotFound("UserNotFound", "User not found.");
            }

            if (user.EmailConfirmed)
            {
                _logger.LogInformation("Resend OTP skipped: User already verified. UserId: {UserId}", user.Id);
                return Error.Validation("AlreadyVerified", "Email is already verified.");
            }

            var lastOtp = await _context.UserOtps
                .Where(o => o.UserId == user.Id)
                .OrderByDescending(o => o.CreatedAt)
                .FirstOrDefaultAsync();

            if (lastOtp != null && lastOtp.CreatedAt > DateTime.UtcNow.AddMinutes(-1))
            {
                _logger.LogWarning("Resend OTP throttled. UserId: {UserId}", user.Id);
                return Error.Validation("PleaseWait", "Please wait a minute before requesting a new code.");
            }

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
                _logger.LogError(ex, "Failed to send OTP email. UserId: {UserId}", user.Id);
            }

            _logger.LogInformation("OTP resent successfully. UserId: {UserId}", user.Id);
            return Result.Ok();
        }
    }
}
