using S2S.Shared.CommonResult;
using S2S.Shared.DataTransferObjects.V1.TranslationDTOs;
using System;
using System.Collections.Generic;
using System.Text;

namespace S2S.ServicesAbstraction
{
	public interface ITranslationHistoryService
	{
		Task<Result<bool>> SaveTextToSignHistoryAsync(string userId, string originalText, string? videoUrl, string? poseUrl, string? sigmlContent);
		Task<Result<bool>> SaveSignToTextHistoryAsync(string userId, string? uploadedVideoUrl, string translatedText, string? generatedAudioUrl);
		Task<Result<List<TranslationHistoryResponseDTO>>> GetUserHistoryAsync(string userId, int pageNumber, int pageSize);
	}
}
