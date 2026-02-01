namespace S2S.Shared.DataTransferObjects.V1.IdentityDTOs
{
    public record ResetPasswordDTO
    {
        public string Token { get; init; } = default!;
        public string NewPassword { get; init; } = default!;
        public string ConfirmPassword { get; init; } = default!;
    }
}
