namespace S2S.Shared.DataTransferObjects.V1.IdentityDTOs
{
	public record UserDTO (string Email, string DisplayName, string Token, string? RefreshToken = null);
}
