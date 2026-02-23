using AutoMapper;
using Google.Apis.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.SqlServer.Server;
using S2S.Domain.Entities.IdentityModule;
using S2S.Persistence.IdentityData.DbContexts;
using S2S.ServicesAbstraction;
using S2S.Shared.CommonResult;
using S2S.Shared.DataTransferObjects.V1.GoogleIdentity;
using S2S.Shared.DataTransferObjects.V1.IdentityDTOs;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace S2S.Services
{
	public class AuthenticationService : IAuthenticationService
	{
		private readonly UserManager<ApplicationUser> _userManager;
		private readonly IConfiguration _configuration;
		private readonly IEmailService _emailService;
		private readonly S2SIdentityDbContext _context;
		private readonly IMapper _mapper;
		private readonly ILogger<AuthenticationService> _logger;

		public AuthenticationService(
			UserManager<ApplicationUser> userManager,
			IConfiguration configuration,
			IEmailService emailService,
			S2SIdentityDbContext context,
			IMapper mapper,
			ILogger<AuthenticationService> logger)
		{
			_userManager = userManager;
			_configuration = configuration;
			_emailService = emailService;
			_context = context;
			_mapper = mapper;
			_logger = logger;
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
			if (User is null)
			{
				_logger.LogWarning("User retrieval failed: User lookup returned null.");
				return Error.NotFound("User.NotFound", $"No User With Email {{{email}}} Was Exist");
			}
			_logger.LogInformation("User retrieved successfully. UserId: {UserId}", User.Id);
			return await MapToUserDTOAsync(User);

		}

		public async Task<Result<UserDTO>> LoginAsync(LoginDTO loginDTO)
		{
			var user = await _userManager.FindByEmailAsync(loginDTO.Email);
			if (user is null)
			{
				_logger.LogWarning("Login failed: User not found.");
				return Error.InvalidCredentails("User.InvalidCredentials");
			}

			// Check if account is locked
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
				// Track failed attempt for lockout
				await _userManager.AccessFailedAsync(user);
				_logger.LogWarning("Login failed: Invalid password. UserId: {UserId}", user.Id);
				return Error.InvalidCredentails("User.InvalidCredentials");
			}

			// Reset failed attempts on successful login
			await _userManager.ResetAccessFailedCountAsync(user);

			var Token = await CreateAccessTokenAsync(user);
			user.RefreshToken = GenerateRefreshToken();
			user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(int.Parse(_configuration["JWTOptions:RefreshTokenExpiryInDays"] ?? "7"));
			user.LastLoginAt = DateTime.UtcNow;
			await _userManager.UpdateAsync(user);
			_logger.LogInformation("Login successful. UserId: {UserId}", user.Id);
			return await MapToUserDTOAsync(user);
		}

		public async Task<Result> RegisterAsync(RegisterDTO registerDTO)
		{
			// Normalize email for duplicate check
			var normalizedEmail = registerDTO.Email.Trim().ToLowerInvariant();

			// Validate SignLanguage is provided if user uses sign language
			if (registerDTO.UsesSignLanguage && !registerDTO.SignLanguage.HasValue)
			{
				_logger.LogWarning("Registration failed: Sign language required but not provided.");
				return Error.Validation("SignLanguage.Required", "Sign language is required when UsesSignLanguage is true");
			}

			// Check if phone number already exists 
			if (await _userManager.Users.AnyAsync(u => u.PhoneNumber == registerDTO.PhoneNumber))
			{
				_logger.LogWarning("Registration failed: Phone number already in use.");
				return Error.Validation("DuplicatePhoneNumber", "Phone number is already in use.");
			}

			// Check if email already exists (with normalized email)
			if (await _userManager.Users.AnyAsync(u => u.NormalizedEmail == normalizedEmail.ToUpperInvariant()))
			{
				_logger.LogWarning("Registration failed: Email already in use.");
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

			if (latestOtp.Attempts >= 3)
			{
				_logger.LogWarning("OTP Verification failed: Max attempts reached previously. UserId: {UserId}", user.Id);
				return Error.Validation("MaxAttemptsReached", "Maximum attempts reached. Please request a new code.");
			}

			if (latestOtp.OtpHash != HashOtp(verifyOtpDTO.Otp))
			{
				latestOtp.Attempts++;
				var remaining = 3 - latestOtp.Attempts;
				if (latestOtp.Attempts >= 3)
				{
					latestOtp.IsUsed = true; // Invalidate OTP after 3 failed attempts
					_logger.LogWarning("OTP Invalidated: Max attempts reached during verification. UserId: {UserId}", user.Id);
				}
				else
				{
					_logger.LogWarning("OTP Verification failed: Invalid code provided. UserId: {UserId}, Remaining: {Remaining}", user.Id, remaining);
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
					var errorCodes = string.Join(", ", identityResult.Errors.Select(e => e.Code));
					_logger.LogError("OTP Verification failed: User update failed. UserId: {UserId}, Errors: {Errors}", user.Id, errorCodes);
					return Error.Failure("UpdateFailed", "Failed to update user status.");
				}

				await transaction.CommitAsync();
				_logger.LogInformation("OTP Verification successful. User verified. UserId: {UserId}", user.Id);
				return await MapToUserDTOAsync(user);
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				_logger.LogError(ex, "Exception occurred during OTP verification transaction. UserId: {UserId}", user.Id);
				throw;
			}
		}

		public async Task<Result<UserDTO>> RefreshTokenAsync(string refreshToken)
		{
			_logger.LogInformation("Processing refresh token request.");

			var user = await _userManager.Users.FirstOrDefaultAsync(u => u.RefreshToken == refreshToken);
			if (user == null || user.RefreshTokenExpiryTime <= DateTime.UtcNow)
			{
				_logger.LogWarning("Refresh token failed: Invalid or expired token.");
				return Error.Unauthorized("InvalidToken", "Invalid or expired refresh token.");
			}

			user.RefreshToken = GenerateRefreshToken();
			user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
			await _userManager.UpdateAsync(user);

			_logger.LogInformation("Token refreshed successfully. UserId: {UserId}", user.Id);
			return await MapToUserDTOAsync(user);
		}

		public async Task<Result> LogoutAsync(string refreshToken)
		{
			var user = await _userManager.Users.FirstOrDefaultAsync(u => u.RefreshToken == refreshToken);
			if (user == null)
			{
				_logger.LogInformation("Logout skipped: User already logged out or invalid token.");
				return Result.Ok();
			}

			user.RefreshToken = null;
			user.RefreshTokenExpiryTime = null;
			await _userManager.UpdateAsync(user);

			_logger.LogInformation("Logout successful. UserId: {UserId}", user.Id);
			return Result.Ok();
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
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to send OTP email. UserId: {UserId}", user.Id);
			}

			_logger.LogInformation("OTP resent successfully. UserId: {UserId}", user.Id);
			return Result.Ok();
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

			var rawToken = GenerateSecureToken();
			var hashedToken = HashOtp(rawToken);

			var otpRecord = new UserOtp
			{
				UserId = user.Id,
				OtpHash = hashedToken,
				ExpiryTime = DateTime.UtcNow.AddMinutes(30),
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

			var hashedToken = HashOtp(resetPasswordDTO.Token);

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

				var otherTokens = await _context.UserOtps
					.Where(o => o.UserId == user.Id && !o.IsUsed)
					.ToListAsync();
				foreach (var t in otherTokens) t.IsUsed = true;

				await _userManager.UpdateSecurityStampAsync(user);

				await _userManager.ResetAccessFailedCountAsync(user);

				await _context.SaveChangesAsync();
				await transaction.CommitAsync();

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

		private string GenerateSecureToken()
		{
			var bytes = new byte[32];
			using var rng = RandomNumberGenerator.Create();
			rng.GetBytes(bytes);
			return Convert.ToHexString(bytes);
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
			var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
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

		public async Task<Result<UserDTO>> LoginWithGoogleAsync(GoogleLoginDTO googleLoginDTO)
		{
			_logger.LogInformation("Processing Google login request.");

			try
			{
				// 1. التحقق من التوكن القادم من جوجل
				var settings = new GoogleJsonWebSignature.ValidationSettings()
				{
					Audience = new List<string> { _configuration["Authentication:Google:ClientId"]! }
				};

				var payload = await GoogleJsonWebSignature.ValidateAsync(googleLoginDTO.IdToken, settings);

				if (payload == null)
				{
					_logger.LogWarning("Google login failed: Invalid token payload.");
					return Error.Unauthorized("InvalidGoogleToken", "Invalid Google Token.");
				}

				// 2. البحث عن المستخدم في قاعدة البيانات
				var user = await _userManager.FindByEmailAsync(payload.Email);

				// 3. إذا كان المستخدم جديداً، نقوم بإنشائه
				if (user == null)
				{
					_logger.LogInformation("New user registering via Google. Email: {Email}", payload.Email);

					user = new ApplicationUser
					{
						Email = payload.Email,
						UserName = payload.Email.Split('@')[0] + Guid.NewGuid().ToString().Substring(0, 4),
						EmailConfirmed = true,

						// --- السطر ده اللي هيحل المشكلة ---
						DisplayName = payload.Name ?? payload.GivenName ?? payload.Email.Split('@')[0],

						// لو عندك حقول تانية (Required) في الداتابيس زي DateOfBirth مثلاً، لازم تديها قيمة افتراضية هنا
						// DateOfBirth = DateOnly.FromDateTime(DateTime.UtcNow) // كمثال لو كان مطلوب
					};

					var createResult = await _userManager.CreateAsync(user);
					if (!createResult.Succeeded)
					{
						var errors = string.Join(", ", createResult.Errors.Select(e => e.Code));
						_logger.LogError("Failed to create user from Google. Errors: {Errors}", errors);
						return Error.Failure("UserCreationFailed", "Failed to create user account.");
					}

					// ربط حساب جوجل بالمستخدم
					await _userManager.AddLoginAsync(user, new UserLoginInfo("Google", payload.Subject, "Google"));
				}

				// 4. توليد الـ JWT الخاص بنظامك (S2S)
				var token = await CreateAccessTokenAsync(user);

				user.RefreshToken = GenerateRefreshToken();
				if (!int.TryParse(_configuration["JWTOptions:RefreshTokenExpiryInDays"], out int expiryDays))
					expiryDays = 7;

				user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(expiryDays);
				user.LastLoginAt = DateTime.UtcNow;

				await _userManager.UpdateAsync(user);

				_logger.LogInformation("Google login successful. UserId: {UserId}", user.Id);

				// 5. استخدام دالة المابينج الخاصة بك لإرجاع البيانات
				return await MapToUserDTOAsync(user);
			}
			catch (InvalidJwtException ex)
			{
				_logger.LogWarning(ex, "Google login failed: Invalid or expired JWT.");
				return Error.Unauthorized("InvalidGoogleToken", "The provided Google token is invalid or expired.");
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Exception occurred during Google login.");
				return Error.Failure("GoogleLoginError", "An unexpected error occurred during Google login.");
			}
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
	}
}
