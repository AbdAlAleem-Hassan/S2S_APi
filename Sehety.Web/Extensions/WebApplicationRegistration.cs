using S2S.Domain.Contracts;

namespace S2S.Web.Extensions
{
	public static class WebApplicationRegistration
	{
		public static async Task<WebApplication> SeedIdentityDatabase(this WebApplication app)
		{
			await using var scope = app.Services.CreateAsyncScope();
			var DataInitializerService = scope.ServiceProvider.GetRequiredKeyedService<IDataInitializer>("Identity");
			await DataInitializerService.InitializeAsync();
			return app;
		}
	}
}
