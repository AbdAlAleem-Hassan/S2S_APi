namespace S2S.Shared.DataTransferObjects.V1.IdentityDTOs
{
    public record UpdateProfileDTO(string DisplayName, string? PhoneNumber = null);
}
