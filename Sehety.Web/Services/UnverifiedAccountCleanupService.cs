using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using S2S.Domain.Entities.IdentityModule;
using S2S.Persistence.IdentityData.DbContexts;
using S2S.Shared.Constants;

namespace S2S.Web.Services
{
    /// <summary>
    /// Periodically removes unverified user accounts (EmailConfirmed == false)
    /// that have exceeded the allowed activation window (default: 24 hours).
    /// All related data (OTPs, password histories, Identity tables) is cascade-deleted.
    /// </summary>
    public sealed class UnverifiedAccountCleanupService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<UnverifiedAccountCleanupService> _logger;
        private readonly bool _enabled;
        private readonly TimeSpan _interval;

        public UnverifiedAccountCleanupService(
            IServiceScopeFactory scopeFactory,
            IConfiguration configuration,
            ILogger<UnverifiedAccountCleanupService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;

            _enabled = configuration.GetValue("AccountCleanup:Enabled", true);
            var intervalMinutes = Math.Clamp(configuration.GetValue("AccountCleanup:IntervalMinutes", 60), 10, 1440);
            _interval = TimeSpan.FromMinutes(intervalMinutes);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_enabled)
            {
                _logger.LogInformation("Unverified account cleanup is disabled.");
                return;
            }

            // Initial delay: wait 2 minutes after app starts to avoid startup load
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
            }
            catch (TaskCanceledException) { return; }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CleanupAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unverified account cleanup failed.");
                }

                try
                {
                    await Task.Delay(_interval, stoppingToken);
                }
                catch (TaskCanceledException) { break; }
            }
        }

        private async Task CleanupAsync(CancellationToken stoppingToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<S2SIdentityDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            var cutoff = DateTime.UtcNow.AddHours(-AuthDefaults.UnverifiedAccountExpiryHours);

            // Find unverified users created before the cutoff
            var expiredUsers = await context.Users
                .Where(u => !u.EmailConfirmed && u.CreatedAt < cutoff)
                .ToListAsync(stoppingToken);

            if (expiredUsers.Count == 0)
                return;

            var deleted = 0;
            foreach (var user in expiredUsers)
            {
                try
                {
                    // UserManager.DeleteAsync handles:
                    // - AspNetUsers row
                    // - AspNetUserRoles, AspNetUserClaims, AspNetUserLogins, AspNetUserTokens (cascade)
                    // - UserOtps, UserPasswordHistories (cascade via FK)
                    var result = await userManager.DeleteAsync(user);
                    if (result.Succeeded)
                    {
                        deleted++;
                    }
                    else
                    {
                        var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                        _logger.LogWarning(
                            "Failed to delete unverified user {UserId}: {Errors}",
                            user.Id, errors);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Exception deleting unverified user {UserId}.", user.Id);
                }
            }

            if (deleted > 0)
            {
                _logger.LogInformation(
                    "Unverified account cleanup: Removed {DeletedCount} accounts older than {ExpiryHours} hours.",
                    deleted, AuthDefaults.UnverifiedAccountExpiryHours);
            }
        }
    }
}
