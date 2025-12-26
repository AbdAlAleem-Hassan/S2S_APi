using Microsoft.AspNetCore.Identity;
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

namespace S2S.Services
{
	public class AuthenticationService : IAuthenticationService
	{
		private readonly UserManager<ApplicationUser> _userManager;
		private readonly IConfiguration _configuration;

		public AuthenticationService(UserManager<ApplicationUser> userManager, IConfiguration configuration)
		{
			_userManager = userManager;
			_configuration = configuration;
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


        public async Task<bool> CheckEmailAsync(string email)
		{
			var User = await _userManager.FindByEmailAsync(email);
			return User is not null;
		}

		public async Task<Result<UserDTO>> GetUserByEmailAsync(string email)
		{
			var User = await _userManager.FindByEmailAsync(email);
			if(User is null)
				return Error.NotFound("User.NotFound", $"No User With Email {{{email}}} Was Exist");
			return new UserDTO(User.Email!, User.DisplayName, await CreateTokenAsync(User));

		}

		public async Task<Result<UserDTO>> LoginAsync(LoginDTO loginDTO) 
		{
			var user = await _userManager.FindByEmailAsync(loginDTO.Email);
			if(user is null)
				return Error.InvalidCredentails("User.InvalidCredentials");
			
			var isPasswordValid = await _userManager.CheckPasswordAsync(user, loginDTO.Password);
			if(!isPasswordValid)
				return Error.InvalidCredentails("User.InvalidCredentials");

			var Token = await CreateTokenAsync(user);
			return new UserDTO(user.Email!, user.DisplayName, Token);
		}

		public async Task<Result<UserDTO>> RegisterAsync(RegisterDTO registerDTO)
		{
            var dobValidation = ValidateDateOfBirth(registerDTO.DateOfBirth);
            if (!dobValidation.IsSuccess)
                return Result<UserDTO>.Fail(dobValidation.Errors.ToList());

            // Validate Enums
            if (!Enum.TryParse<UserType>(registerDTO.UserType, out var userType))
                return Error.Validation("UserType.Invalid", "Invalid user type");

            if (!Enum.TryParse<SignLanguage>(registerDTO.SignLanguage, out var signLanguage))
                return Error.Validation("SignLanguage.Invalid", "Invalid sign language");
            var user = new ApplicationUser
			{
				Email = registerDTO.Email,
				UserName = registerDTO.UserName,
				DisplayName = registerDTO.DisplayName,
                DateOfBirth = registerDTO.DateOfBirth, //"YYYY-MM-DD"
                PhoneNumber = registerDTO.PhoneNumber,
				UserType = userType,
				UsesSignLanguage = registerDTO.UsesSignLanguage,
				SignLanguage =signLanguage
				
			};

			var identityResult = await _userManager.CreateAsync(user, registerDTO.Password);

			if (identityResult.Succeeded)
			{
				var Token = await CreateTokenAsync(user);
				return new UserDTO(user.Email!, user.DisplayName, Token);
			}
			
			return identityResult.Errors.Select(e => Error.Validation(e.Code, e.Description)).ToList();
		}
	
		private async Task<string> CreateTokenAsync(ApplicationUser user)
		{
			var claims = new List<Claim>()
			{
				new Claim(JwtRegisteredClaimNames.Email, user.Email!),
				new Claim(JwtRegisteredClaimNames.Name, user.UserName!)
			};

			var roles = await _userManager.GetRolesAsync(user);
			foreach (var role in roles)
			{
				claims.Add(new Claim(ClaimTypes.Role, role));
			}

			var secretKey = _configuration["JWTOptions:SecretKey"];
			var key  = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
			var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

			var Token = new JwtSecurityToken(
				issuer: _configuration["JWTOptions:Issuer"],
				audience: _configuration["JWTOptions:Audience"],
				expires: DateTime.Now.AddDays(1),
				claims: claims,
				signingCredentials: creds
				);

			return new JwtSecurityTokenHandler().WriteToken(Token);
		}
	}
}
