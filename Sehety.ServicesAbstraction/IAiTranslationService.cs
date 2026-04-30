using Microsoft.AspNetCore.Http;
using S2S.Shared.CommonResult;
using System;
using System.Collections.Generic;
using System.Text;

namespace S2S.ServicesAbstraction
{
	public interface IAiTranslationService
	{
		Task<Result<string>> SendSignToTextAsync(IFormFile video, string language, bool includeAudio);
		Task<Result<string>> SendTextToSignAsync(string text, string avatar, string speed, string outputFormat);
		Task<Result<string>> SendAudioToSignAsync(IFormFile audio, string avatar, string speed, string outputFormat);
		Task<Result<string>> DownloadAndSaveMediaAsync(string fileName, string type);
	}
}
