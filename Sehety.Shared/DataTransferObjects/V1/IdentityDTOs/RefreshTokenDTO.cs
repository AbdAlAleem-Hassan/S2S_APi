using System.ComponentModel.DataAnnotations;

namespace S2S.Shared.DataTransferObjects.V1.IdentityDTOs
{
    public record RefreshTokenDTO(
        [StringLength(500, ErrorMessage = "Refresh token cannot exceed 500 characters")]
        string? RefreshToken
    );
}
