using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace S2S.Web.Services
{
	public sealed class MediaCleanupService : BackgroundService
	{
		private static readonly string[] MediaFolders = { "audio", "pose", "video", "profile" };

		private readonly IWebHostEnvironment _env;
		private readonly ILogger<MediaCleanupService> _logger;
		private readonly bool _enabled;
		private readonly TimeSpan _interval;
		private readonly TimeSpan _retention;

		public MediaCleanupService(
			IWebHostEnvironment env,
			IConfiguration configuration,
			ILogger<MediaCleanupService> logger)
		{
			_env = env;
			_logger = logger;

			_enabled = configuration.GetValue("MediaCleanup:Enabled", true);
			var intervalMinutes = Math.Clamp(configuration.GetValue("MediaCleanup:IntervalMinutes", 60), 5, 1440);
			var retentionDays = Math.Clamp(configuration.GetValue("MediaCleanup:RetentionDays", 7), 1, 365);

			_interval = TimeSpan.FromMinutes(intervalMinutes);
			_retention = TimeSpan.FromDays(retentionDays);
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			if (!_enabled)
			{
				_logger.LogInformation("Media cleanup is disabled.");
				return;
			}

			while (!stoppingToken.IsCancellationRequested)
			{
				try
				{
					CleanupOnce();
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Media cleanup failed.");
				}

				try
				{
					await Task.Delay(_interval, stoppingToken);
				}
				catch (TaskCanceledException)
				{
					break;
				}
			}
		}

		private void CleanupOnce()
		{
			var webRootPath = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
			var mediaRoot = Path.Combine(webRootPath, "media");
			if (!Directory.Exists(mediaRoot))
			{
				return;
			}

			var cutoff = DateTime.UtcNow - _retention;
			var deleted = 0;

			foreach (var folder in MediaFolders)
			{
				var folderPath = Path.Combine(mediaRoot, folder);
				if (!Directory.Exists(folderPath))
				{
					continue;
				}

				foreach (var file in Directory.EnumerateFiles(folderPath, "*", SearchOption.TopDirectoryOnly))
				{
					try
					{
						var info = new FileInfo(file);
						if (info.LastWriteTimeUtc < cutoff)
						{
							info.Delete();
							deleted++;
						}
					}
					catch (Exception ex)
					{
						_logger.LogWarning(ex, "Failed to delete media file {FileName}.", Path.GetFileName(file));
					}
				}
			}

			if (deleted > 0)
			{
				_logger.LogInformation(
					"Media cleanup removed {DeletedCount} files older than {RetentionDays} days.",
					deleted,
					_retention.TotalDays);
			}
		}
	}
}
