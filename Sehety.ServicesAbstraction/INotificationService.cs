namespace S2S.ServicesAbstraction
{
	public interface INotificationService
	{
		Task<bool> SendNotificationAsync(string deviceToken, string title, string body, Dictionary<string, string>? data = null);
	}
}
