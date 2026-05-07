namespace S2S.Shared.DataTransferObjects.V1.IdentityDTOs
{
    public record VerifyOtpDTO(
        string Email,

        string Otp
    );
}
