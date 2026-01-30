using AutoMapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using S2S.Domain.Entities.Enums;
using S2S.Domain.Entities.IdentityModule;
using S2S.ServicesAbstraction;
using S2S.Shared.CommonResult;
using S2S.Shared.DataTransferObjects.V1.IdentityDTOs;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using S2S.Persistence.DbContexts;
using S2S.Persistence.IdentityData.DbContexts;
using System.Security.Cryptography;

namespace S2S.Services
{
	public class AuthenticationService : IAuthenticationService
	{
		private readonly UserManager<ApplicationUser> _userManager;
		private readonly IConfiguration _configuration;
        private readonly IEmailService _emailService;
        private readonly S2SIdentityDbContext _context;
        private readonly IMapper _mapper;

		public AuthenticationService(
            UserManager<ApplicationUser> userManager, 
            IConfiguration configuration,
            IEmailService emailService,
            S2SIdentityDbContext context,
            IMapper mapper)
		{
			_userManager = userManager;
			_configuration = configuration;
            _emailService = emailService;
            _context = context;
            _mapper = mapper;
		}

        private Result ValidateDateOfBirth(DateOnly? dateOfBirth)
        {
            if (!dateOfBirth.HasValue)
                return Result.Ok();

            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            if (dateOfBirth > today)
                return Result.Fail(Error.Validation("DateOfBirth.Future",
					"Date of birth cannot be in the future"));
            var age = today.Year - dateOfBirth.Value.Year;
            if (dateOfBirth.Value > today.AddYears(-age))
                age--;

            if (age < 7)
                return Result.Fail(Error.Validation(
                    "DateOfBirth.TooYoung",
                    "User must be at least 13 years old"));

            return Result.Ok();
        }


/*
        public async Task<bool> CheckEmailAsync(string email)
		{
			var User = await _userManager.FindByEmailAsync(email);
			return User is not null;
		}
*/

		public async Task<Result<UserDTO>> GetUserByEmailAsync(string email)
		{
			var User = await _userManager.FindByEmailAsync(email);
			if(User is null)
				return Error.NotFound("User.NotFound", $"No User With Email {{{email}}} Was Exist");
			return await MapToUserDTOAsync(User);

		}

		public async Task<Result<UserDTO>> LoginAsync(LoginDTO loginDTO) 
		{
			var user = await _userManager.FindByEmailAsync(loginDTO.Email);
			if(user is null)
				return Error.InvalidCredentails("User.InvalidCredentials");

            if (!user.EmailConfirmed)
                return Error.Unauthorized("EmailNotConfirmed", "Please verify your email first.");
			
			var isPasswordValid = await _userManager.CheckPasswordAsync(user, loginDTO.Password);
			if(!isPasswordValid)
				return Error.InvalidCredentails("User.InvalidCredentials");

			var Token = await CreateAccessTokenAsync(user);
            user.RefreshToken = GenerateRefreshToken();
            user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(int.Parse(_configuration["JWTOptions:RefreshTokenExpiryInDays"] ?? "7"));
            user.LastLoginAt = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);

			return await MapToUserDTOAsync(user);
		}

		public async Task<Result> RegisterAsync(RegisterDTO registerDTO)
		{
            // Normalize email for duplicate check
            var normalizedEmail = registerDTO.Email.Trim().ToLowerInvariant();

            var dobValidation = ValidateDateOfBirth(registerDTO.DateOfBirth);
            if (!dobValidation.IsSuccess)
                return Result.Fail(dobValidation.Errors.ToList());

            // Validate SignLanguage is provided if user uses sign language
            if (registerDTO.UsesSignLanguage && !registerDTO.SignLanguage.HasValue)
                return Error.Validation("SignLanguage.Required", "Sign language is required when UsesSignLanguage is true");

			// Check if phone number already exists 
			if (await _userManager.Users.AnyAsync(u => u.PhoneNumber == registerDTO.PhoneNumber))
			{
				return Error.Validation("DuplicatePhoneNumber", "Phone number is already in use.");
			}

            // Check if email already exists (with normalized email)
            if (await _userManager.Users.AnyAsync(u => u.NormalizedEmail == normalizedEmail.ToUpperInvariant()))
            {
                return Error.Validation("DuplicateEmail", "Email is already in use.");
            }

            // Use AutoMapper for DTO to Entity mapping
            var user = _mapper.Map<ApplicationUser>(registerDTO);

			var identityResult = await _userManager.CreateAsync(user, registerDTO.Password);

			if (identityResult.Succeeded)
			{
                var otpCode = GenerateOtp();
                var otpRecord = new UserOtp
                {
                    UserId = user.Id,
                    OtpHash = HashOtp(otpCode),
                    ExpiryTime = DateTime.UtcNow.AddMinutes(10),
                    Attempts = 0,
                    IsUsed = false
                };

                _context.UserOtps.Add(otpRecord);
                await _context.SaveChangesAsync();

                try
                {
                    await _emailService.SendOtpEmailAsync(user.Email!, otpCode);
                }
                catch
                {
                    
                }

				return Result.Ok();
			}
			
			return identityResult.Errors.Select(e => Error.Validation(e.Code, e.Description)).ToList();
		}

        public async Task<Result<UserDTO>> VerifyOtpAsync(VerifyOtpDTO verifyOtpDTO)
        {
            var user = await _userManager.FindByEmailAsync(verifyOtpDTO.Email);
            if (user == null) return Error.NotFound("UserNotFound", "User not found.");

            if (user.EmailConfirmed) return Error.Validation("AlreadyVerified", "Email is already verified.");

            var latestOtp = await _context.UserOtps
                .Where(o => o.UserId == user.Id && !o.IsUsed && o.ExpiryTime > DateTime.UtcNow)
                .OrderByDescending(o => o.CreatedAt)
                .FirstOrDefaultAsync();

            if (latestOtp == null) return Error.Validation("InvalidOtp", "No active verification code found.");

            if (latestOtp.Attempts >= 3) return Error.Validation("MaxAttemptsReached", "Maximum attempts reached. Please request a new code.");

            if (latestOtp.OtpHash != HashOtp(verifyOtpDTO.Otp))
            {
                latestOtp.Attempts++;
                if (latestOtp.Attempts >= 3)
                {
                    latestOtp.IsUsed = true; // Invalidate OTP after 3 failed attempts
                }
                await _context.SaveChangesAsync();
                return Error.Validation("WrongOtp", $"Invalid verification code. Remaining attempts: {3 - latestOtp.Attempts}");
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                latestOtp.IsUsed = true;
                user.EmailConfirmed = true;
                user.RefreshToken = GenerateRefreshToken();
                user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(int.Parse(_configuration["JWTOptions:RefreshTokenExpiryInDays"] ?? "7"));

                await _context.SaveChangesAsync();
                var identityResult = await _userManager.UpdateAsync(user);

                if (!identityResult.Succeeded)
                {
                    await transaction.RollbackAsync();
                    return Error.Failure("UpdateFailed", "Failed to update user status.");
                }

                await transaction.CommitAsync();

                return await MapToUserDTOAsync(user);
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<Result<UserDTO>> RefreshTokenAsync(string refreshToken)
        {
            var user = await _userManager.Users.FirstOrDefaultAsync(u => u.RefreshToken == refreshToken);
            if (user == null || user.RefreshTokenExpiryTime <= DateTime.UtcNow)
                return Error.Unauthorized("InvalidToken", "Invalid or expired refresh token.");

            // Rotate Refresh Token
            user.RefreshToken = GenerateRefreshToken();
            user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
            await _userManager.UpdateAsync(user);

            return await MapToUserDTOAsync(user);
        }

        public async Task<Result> LogoutAsync(string refreshToken)
        {
            var user = await _userManager.Users.FirstOrDefaultAsync(u => u.RefreshToken == refreshToken);
            if (user == null) return Result.Ok(); // Already logged out or invalid token

            user.RefreshToken = null;
            user.RefreshTokenExpiryTime = null;
            await _userManager.UpdateAsync(user);

            return Result.Ok();
        }

        public async Task<Result> ResendOtpAsync(string email)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null) return Error.NotFound("UserNotFound", "User not found.");

            if (user.EmailConfirmed) return Error.Validation("AlreadyVerified", "Email is already verified.");

            
            var lastOtp = await _context.UserOtps
                .Where(o => o.UserId == user.Id)
                .OrderByDescending(o => o.CreatedAt)
                .FirstOrDefaultAsync();

            if (lastOtp != null && lastOtp.CreatedAt > DateTime.UtcNow.AddMinutes(-1))
            {
                return Error.Validation("PleaseWait", "Please wait a minute before requesting a new code.");
            }

            var otpCode = GenerateOtp();
            var otpRecord = new UserOtp
            {
                UserId = user.Id,
                OtpHash = HashOtp(otpCode),
                ExpiryTime = DateTime.UtcNow.AddMinutes(10),
                Attempts = 0,
                IsUsed = false
            };

            _context.UserOtps.Add(otpRecord);
            await _context.SaveChangesAsync();

            await _emailService.SendOtpEmailAsync(user.Email!, otpCode);

            return Result.Ok();
        }

        private string GenerateOtp() => RandomNumberGenerator.GetInt32(100000, 999999).ToString();

        private string HashOtp(string otp)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(otp));
            return Convert.ToBase64String(bytes);
        }

        private string GenerateRefreshToken()
        {
            var randomNumber = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);
            return Convert.ToBase64String(randomNumber);
        }

        
        private static string SanitizeInput(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            
            var sanitized = input.Trim();

            // Remove potentially dangerous HTML/Script characters
            sanitized = sanitized
                .Replace("<", "")
                .Replace(">", "")
                .Replace("\"", "")
                .Replace("'", "")
                .Replace("&", "")
                .Replace(";", "")
                .Replace("(", "")
                .Replace(")", "")
                .Replace("{", "")
                .Replace("}", "");

            return sanitized;
        }

        
        private async Task<UserDTO> MapToUserDTOAsync(ApplicationUser user)
        {
            var token = await CreateAccessTokenAsync(user);
            var userDTO = _mapper.Map<UserDTO>(user);
            return userDTO with { Token = token };
        }

		private async Task<string> CreateAccessTokenAsync(ApplicationUser user)
		{
			var claims = new List<Claim>()
			{
				new Claim(JwtRegisteredClaimNames.Email, user.Email!),
				new Claim(JwtRegisteredClaimNames.Name, user.UserName!),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
			};

			var roles = await _userManager.GetRolesAsync(user);
			foreach (var role in roles)
			{
				claims.Add(new Claim(ClaimTypes.Role, role));
			}

			var secretKey = _configuration["JWTOptions:SecretKey"]!;
			var key  = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
			var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

			var Token = new JwtSecurityToken(
				issuer: _configuration["JWTOptions:Issuer"],
				audience: _configuration["JWTOptions:Audience"],
				expires: DateTime.UtcNow.AddMinutes(int.Parse(_configuration["JWTOptions:AccessTokenExpiryInMinutes"] ?? "15")),
				claims: claims,
				signingCredentials: creds
				);

			return new JwtSecurityTokenHandler().WriteToken(Token);
		}
	}
}
