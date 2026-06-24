namespace S2S.Shared.DataTransferObjects.V1.TranslationDTOs
{
    public class UserUsageInfo
    {
        public int Used { get; set; }
        public int Limit { get; set; }
        public int Remaining => IsUnlimited ? -1 : Math.Max(0, Limit - Used);
        public DateTime ResetsAt { get; set; }
        public bool IsUnlimited { get; set; }
        public string Tier { get; set; } = "Free";
    }
}
