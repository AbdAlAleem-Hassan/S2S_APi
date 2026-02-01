using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using S2S.ServicesAbstraction;
using S2S.Shared.CommonResult; // تأكد من وجود هذا الـ Namespace
using System.Net.Http.Headers;

namespace S2S.Services
{
	public class AiTranslationService : IAiTranslationService
	{
		private readonly HttpClient _client;

		public AiTranslationService(HttpClient client, IConfiguration config)
		{
			_client = client;
			_client.BaseAddress = new Uri(config["AISettings:BaseUrl"]);
		}

		public async Task<Result<string>> SendSignToTextAsync(IFormFile video, string language, bool includeAudio)
		{
			// 1. Validation (مثلما تفعل في AuthenticationService)
			if (video is null || video.Length == 0)
				return Error.Validation("Video.Empty", "Video file is required.");

			try
			{
				using var content = new MultipartFormDataContent();

				var fileContent = new StreamContent(video.OpenReadStream());
				fileContent.Headers.ContentType = new MediaTypeHeaderValue(video.ContentType);
				content.Add(fileContent, "video_file", video.FileName);

				if (!string.IsNullOrEmpty(language))
					content.Add(new StringContent(language), "language");

				content.Add(new StringContent(includeAudio.ToString().ToLower()), "include_audio");

				var response = await _client.PostAsync("translate/sign-to-text", content);

				// 2. Handling External API Errors
				if (!response.IsSuccessStatusCode)
				{
					var errorDetails = await response.Content.ReadAsStringAsync();
					return Error.Failure("AiServer.Error", $"AI Server Error ({response.StatusCode}): {errorDetails}");
				}

				var resultString = await response.Content.ReadAsStringAsync();
				return resultString; // Implicit conversion to Result<string> usually works, or new Result<string>(resultString)
			}
			catch (Exception ex)
			{
				// 3. Handling Connection Issues
				return Error.Failure("AiServer.Connection", $"Failed to connect to AI Server: {ex.Message}");
			}
		}

		public async Task<Result<string>> SendTextToSignAsync(string text, string avatar, string speed)
		{
			if (string.IsNullOrWhiteSpace(text))
				return Error.Validation("Text.Empty", "Text is required for translation.");

			try
			{
				using var content = new MultipartFormDataContent();

				content.Add(new StringContent(text), "text");
				content.Add(new StringContent(avatar ?? "default"), "avatar");
				content.Add(new StringContent(speed ?? "1.0"), "speed");

				var response = await _client.PostAsync("translate/to-sign", content);

				if (!response.IsSuccessStatusCode)
				{
					var errorDetails = await response.Content.ReadAsStringAsync();
					return Error.Failure("AiServer.Error", $"AI Server Error ({response.StatusCode}): {errorDetails}");
				}

				return await response.Content.ReadAsStringAsync();
			}
			catch (Exception ex)
			{
				return Error.Failure("AiServer.Connection", $"Failed to connect to AI Server: {ex.Message}");
			}
		}

		public async Task<Result<string>> SendAudioToSignAsync(IFormFile audio, string avatar, string speed)
		{
			if (audio is null || audio.Length == 0)
				return Error.Validation("Audio.Empty", "Audio file is required.");

			try
			{
				using var content = new MultipartFormDataContent();

				var fileContent = new StreamContent(audio.OpenReadStream());
				fileContent.Headers.ContentType = new MediaTypeHeaderValue(audio.ContentType);
				content.Add(fileContent, "audio_file", audio.FileName);

				content.Add(new StringContent(avatar ?? "default"), "avatar");
				content.Add(new StringContent(speed ?? "1.0"), "speed");

				var response = await _client.PostAsync("translate/to-sign", content);

				if (!response.IsSuccessStatusCode)
				{
					var errorDetails = await response.Content.ReadAsStringAsync();
					return Error.Failure("AiServer.Error", $"AI Server Error ({response.StatusCode}): {errorDetails}");
				}

				return await response.Content.ReadAsStringAsync();
			}
			catch (Exception ex)
			{
				return Error.Failure("AiServer.Connection", $"Failed to connect to AI Server: {ex.Message}");
			}
		}
	}
}