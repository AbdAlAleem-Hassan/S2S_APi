using AutoMapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using S2S.Domain.Entities.IdentityModule;
using S2S.Persistence.IdentityData.DbContexts;
using S2S.ServicesAbstraction;
using S2S.Shared.CommonResult;
using S2S.Shared.Constants;
using S2S.Shared.DataTransferObjects.V1.IdentityDTOs;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace S2S.Services
{
    public class TokenService : ITokenService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IConfiguration _configuration;
        private readonly S2SIdentityDbContext _context;
        private readonly IMapper _mapper;
        private readonly ILogger<TokenService> _logger;

        public TokenService(
            UserManager<ApplicationUser> userManager,
            IConfiguration configuration,
            S2SIdentityDbContext context,
            IMapper mapper,
            ILogger<TokenService> logger)
        {
            _userManager = userManager;
            _configuration = configuration;
            _context = context;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<Result<UserDTO>> RefreshTokenAsync(string refreshToken)
        {
            _logger.LogInformation("Processing refresh token request.");

            // Hash the incoming token and look up by hash (never store plaintext)
            var hashedToken = AuthHelpers.HashRefreshToken(refreshToken);
            var user = await _userManager.Users.FirstOrDefaultAsync(u => u.RefreshToken == hashedToken);
            if (user == null || user.RefreshTokenExpiryTime <= DateTime.UtcNow)
            {
                _logger.LogWarning("Refresh token failed: Invalid or expired token.");
                return Error.Unauthorized("InvalidToken", "Invalid or expired refresh token.");
            }

            // Rotate: generate new plaintext, store only the hash
            var rawRefreshToken = AuthHelpers.GenerateRefreshToken();
            user.RefreshToken = AuthHelpers.HashRefreshToken(rawRefreshToken);
            user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(AuthDefaults.RefreshTokenExpiryDays);
            await _userManager.UpdateAsync(user);

            _logger.LogInformation("Token refreshed successfully. UserId: {UserId}", user.Id);
            return await MapToUserDTOAsync(user, rawRefreshToken);
        }

        public async Task<Result> LogoutAsync(string refreshToken)
        {
            var hashedToken = AuthHelpers.HashRefreshToken(refreshToken);
            var user = await _userManager.Users.FirstOrDefaultAsync(u => u.RefreshToken == hashedToken);
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

        public async Task<string> CreateAccessTokenAsync(ApplicationUser user)
        {
            // Use only standard JWT claim names — no ASP.NET schema URLs
            // sub = user ID, email, name, jti, role (short names only)
            var claims = new List<Claim>()
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id),
                new Claim(JwtRegisteredClaimNames.Email, user.Email!),
                new Claim(JwtRegisteredClaimNames.Name, user.UserName!),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            };

            var roles = await _userManager.GetRolesAsync(user);
            foreach (var role in roles)
            {
                claims.Add(new Claim("role", role));
            }

            var secretKey = _configuration["JWTOptions:SecretKey"]!;
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _configuration["JWTOptions:Issuer"],
                audience: _configuration["JWTOptions:Audience"],
                expires: DateTime.UtcNow.AddMinutes(int.Parse(_configuration["JWTOptions:AccessTokenExpiryInMinutes"] ?? AuthDefaults.AccessTokenExpiryMinutes.ToString())),
                claims: claims,
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public async Task<UserDTO> MapToUserDTOAsync(ApplicationUser user, string? rawRefreshToken = null)
        {
            var token = await CreateAccessTokenAsync(user);
            var userDTO = _mapper.Map<UserDTO>(user);
            // Return plaintext refresh token (not the hash stored in DB)
            return userDTO with { Token = token, RefreshToken = rawRefreshToken };
        }
    }
}
