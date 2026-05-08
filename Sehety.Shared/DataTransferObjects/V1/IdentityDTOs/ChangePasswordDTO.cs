namespace S2S.Shared.DataTransferObjects.V1.IdentityDTOs
{
    public record ChangePasswordDTO(
        string CurrentPassword,

        string NewPassword,

        string ConfirmNewPassword
    );
}
