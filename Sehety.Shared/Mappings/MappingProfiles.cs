using AutoMapper;
using S2S.Domain.Entities.Enums;
using S2S.Domain.Entities.IdentityModule;
using S2S.Shared.DataTransferObjects.V1.IdentityDTOs;

namespace S2S.Shared.Mappings
{
    public class MappingProfiles : Profile
    {
        public MappingProfiles()
        {
            // RegisterDTO -> ApplicationUser
            CreateMap<RegisterDTO, ApplicationUser>()
                .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.Email.Trim().ToLowerInvariant()))
                .ForMember(dest => dest.UserName, opt => opt.MapFrom(src => src.UserName.Trim()))
                .ForMember(dest => dest.DisplayName, opt => opt.MapFrom(src => SanitizeInput(src.DisplayName)))
                .ForMember(dest => dest.DateOfBirth, opt => opt.MapFrom(src => src.DateOfBirth))
                .ForMember(dest => dest.PhoneNumber, opt => opt.MapFrom(src => src.PhoneNumber))
                .ForMember(dest => dest.UserType, opt => opt.MapFrom(src => src.UserType))
                .ForMember(dest => dest.UsesSignLanguage, opt => opt.MapFrom(src => src.UsesSignLanguage))
                .ForMember(dest => dest.SignLanguage, opt => opt.MapFrom(src => src.SignLanguage ?? SignLanguage.Egyptian))
                .ForMember(dest => dest.EmailConfirmed, opt => opt.MapFrom(_ => false))
                // Ignore computed/identity-managed fields
                
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.NormalizedEmail, opt => opt.Ignore())
                .ForMember(dest => dest.NormalizedUserName, opt => opt.Ignore())
                .ForMember(dest => dest.PasswordHash, opt => opt.Ignore())
                .ForMember(dest => dest.SecurityStamp, opt => opt.Ignore())
                .ForMember(dest => dest.ConcurrencyStamp, opt => opt.Ignore())
                .ForMember(dest => dest.PhoneNumberConfirmed, opt => opt.Ignore())
                .ForMember(dest => dest.TwoFactorEnabled, opt => opt.Ignore())
                .ForMember(dest => dest.LockoutEnd, opt => opt.Ignore())
                .ForMember(dest => dest.LockoutEnabled, opt => opt.Ignore())
                .ForMember(dest => dest.AccessFailedCount, opt => opt.Ignore())
                .ForMember(dest => dest.ProfileImageUrl, opt => opt.Ignore())
                .ForMember(dest => dest.IsActive, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.LastLoginAt, opt => opt.Ignore())
                .ForMember(dest => dest.IsFirstLogin, opt => opt.Ignore())
                .ForMember(dest => dest.Address, opt => opt.Ignore())
                .ForMember(dest => dest.RefreshToken, opt => opt.Ignore())
                .ForMember(dest => dest.RefreshTokenExpiryTime, opt => opt.Ignore())
                .ForMember(dest => dest.Otps, opt => opt.Ignore());

            // ApplicationUser -> UserDTO
            // Note: Token and RefreshToken are set manually after mapping since they require external logic
            CreateMap<ApplicationUser, UserDTO>()
                .ForCtorParam("Email", opt => opt.MapFrom(src => src.Email!))
                .ForCtorParam("DisplayName", opt => opt.MapFrom(src => src.DisplayName))
                .ForCtorParam("Token", opt => opt.MapFrom(_ => string.Empty)) // Will be set manually
                .ForCtorParam("RefreshToken", opt => opt.MapFrom(src => src.RefreshToken));
        }

        
        private static string SanitizeInput(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            var sanitized = input.Trim();

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
    }
}
