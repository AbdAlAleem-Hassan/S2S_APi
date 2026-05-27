using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using S2S.Domain.Entities.Translation;
using S2S.Persistence.IdentityData.DbContexts;
using S2S.ServicesAbstraction;
using S2S.Shared.CommonResult;
using S2S.Shared.DataTransferObjects.V1.TranslationDTOs;
using System;
using System.Collections.Generic;
using System.Text;

namespace S2S.Services
{
	public class TranslationHistoryService : ITranslationHistoryService
	{
		private readonly S2SIdentityDbContext _db;
		private readonly ILogger<TranslationHistoryService> _logger;

		public TranslationHistoryService(S2SIdentityDbContext db, ILogger<TranslationHistoryService> logger)
		{
			_db = db;
			_logger = logger;
		}

		public async Task<Result<bool>> SaveTextToSignHistoryAsync(string userId, string originalText, string? videoUrl, string? poseUrl, string? sigmlContent)
		{
			try
			{
				var historyRecord = new TranslationHistory
				{
					UserId = userId,
					ArabicInputText = originalText,
					VideoUrl = videoUrl,
					PoseUrl = poseUrl,
					SigmlContent = sigmlContent
				};

				await _db.TranslationHistories.AddAsync(historyRecord);
				await _db.SaveChangesAsync();

				_logger.LogInformation("Text-to-Sign history saved for user {UserId}", userId);
				return Result<bool>.Ok(true);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to save text-to-sign history.");
				return Error.Failure("History.SaveFailed", "Could not save history.");
			}
		}

		public async Task<Result<bool>> SaveSignToTextHistoryAsync(string userId, string? uploadedVideoUrl, string translatedText, string? generatedAudioUrl)
		{
			try
			{
				var historyRecord = new TranslationHistory
				{
					UserId = userId,
					VideoUrl = uploadedVideoUrl,
					ArabicInputText = translatedText,
					AudioUrl = generatedAudioUrl
				};

				await _db.TranslationHistories.AddAsync(historyRecord);
				await _db.SaveChangesAsync();

				_logger.LogInformation("Sign-to-Text history saved for user {UserId}", userId);
				return Result<bool>.Ok(true);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to save sign-to-text history.");
				return Error.Failure("History.SaveFailed", "Could not save history.");
			}
		}
		public async Task<Result<List<TranslationHistoryResponseDTO>>> GetUserHistoryAsync(string userId, int pageNumber, int pageSize)
		{
			try
			{
				_logger.LogInformation("Fetching history for user: {UserId}, Page: {Page}, Size: {Size}", userId, pageNumber, pageSize);

				// التأكد من أن قيم الـ Pagination منطقية
				pageNumber = pageNumber < 1 ? 1 : pageNumber;
				pageSize = pageSize < 1 ? 10 : pageSize;

				var historyList = await _db.TranslationHistories
					.Where(h => h.UserId == userId)          // فحص الـ ID كـ string
					.OrderByDescending(h => h.CreatedAt)     // الترتيب من الأحدث للأقدم
					.Skip((pageNumber - 1) * pageSize)       // تخطي الصفحات السابقة
					.Take(pageSize)                          // أخذ حجم الصفحة الحالية فقط
					.Select(h => new TranslationHistoryResponseDTO
					{
						Id = h.Id,
						ArabicInputText = h.ArabicInputText,
						VideoUrl = h.VideoUrl,
						PoseUrl = h.PoseUrl,
						AudioUrl = h.AudioUrl,
						SigmlContent = h.SigmlContent, // 👈 السطر السحري ده اللي كان ناقص ومسبّب الأزمة!
						CreatedAt = h.CreatedAt
					})
					.ToListAsync();

				return Result<List<TranslationHistoryResponseDTO>>.Ok(historyList);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to retrieve translation history for user {UserId}", userId);
				return Error.Failure("History.FetchFailed", "Could not retrieve translation history.");
			}
		}
	}
}
