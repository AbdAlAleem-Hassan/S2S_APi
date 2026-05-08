using Microsoft.Extensions.Diagnostics.HealthChecks;
using S2S.Persistence.IdentityData.DbContexts;

namespace S2S.Web.Health
{
    public sealed class DatabaseHealthCheck : IHealthCheck
    {
        private readonly S2SIdentityDbContext _dbContext;

        public DatabaseHealthCheck(S2SIdentityDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var canConnect = await _dbContext.Database.CanConnectAsync(cancellationToken);
                return canConnect
                    ? HealthCheckResult.Healthy("Database is reachable.")
                    : HealthCheckResult.Unhealthy("Cannot connect to database.");
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy("Database check failed.", ex);
            }
        }
    }
}
