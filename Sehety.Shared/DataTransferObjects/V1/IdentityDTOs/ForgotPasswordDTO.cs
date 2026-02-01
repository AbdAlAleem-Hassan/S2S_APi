namespace S2S.Shared.DataTransferObjects.V1.IdentityDTOs
{
    public record ForgotPasswordDTO
    {
        public string Email { get; init; } = default!;
    }
}
