using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using S2S.ServicesAbstraction;
using S2S.Shared.CommonResult; 
using System.Net.Http.Headers;

namespace S2S.Services
{
	public class AiTranslationService : IAiTranslationService
	{
		private readonly HttpClient _client;
		private readonly ILogger<AiTranslationService> _logger;

		public AiTranslationService(HttpClient client, IConfiguration config, ILogger<AiTranslationService> logger)
		{
			_client = client;
			_logger = logger;
			_client.BaseAddress = new Uri(config["AISettings:BaseUrl"]);

			var hfToken = config["AISettings:HFToken"];
			if (!string.IsNullOrEmpty(hfToken))
			{
				_client.DefaultRequestHeaders.Authorization =
					new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", hfToken);
			}

			var apiKey = config["AISettings:ApiKey"];
			if (!string.IsNullOrEmpty(apiKey))
			{
				if (_client.DefaultRequestHeaders.Contains("x-api-key"))
					_client.DefaultRequestHeaders.Remove("x-api-key");

				_client.DefaultRequestHeaders.Add("x-api-key", apiKey);
			}
		}

		public async Task<Result<string>> SendSignToTextAsync(IFormFile video, string language, bool includeAudio)
		{
			_logger.LogInformation("Starting SendSignToTextAsync. Language: {Language}, IncludeAudio: {IncludeAudio}", language, includeAudio);

			// 1. Validation (مثلما تفعل في AuthenticationService)
			if (video is null || video.Length == 0)
			{
				_logger.LogWarning("SignToText validation failed: Video file is null or empty.");
				return Error.Validation("Video.Empty", "Video file is required.");
			}

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
					var body = await response.Content.ReadAsStringAsync();
					
					_logger.LogError("AI Server returned error. StatusCode: {StatusCode}, Details: {Details}",
					response.StatusCode, body);
					
					return Error.Failure("AiServer.Error", $"AI Server Error ({response.StatusCode}): {body}");
				}

				var resultString = await response.Content.ReadAsStringAsync();
				
				_logger.LogInformation("SignToText translation completed successfully for file: {FileName}", video.FileName);
				
				return resultString; // Implicit conversion to Result<string> usually works, or new Result<string>(resultString)
			}
			catch (Exception ex)
			{
				// 3. Handling Connection Issues
				_logger.LogError(ex, "Exception occurred during SignToText translation for file: {FileName}", video.FileName);
				return Error.Failure("AiServer.Connection", $"Failed to connect to AI Server: {ex.Message}");
			}
		}

		public async Task<Result<string>> SendTextToSignAsync(string text, string avatar, string speed)
		{
			_logger.LogInformation("Starting TextToSign request. TextLength: {Length}, Avatar: {Avatar}, Speed: {Speed}",
				text?.Length ?? 0, avatar, speed);
			if (string.IsNullOrWhiteSpace(text))
			{
				_logger.LogWarning("TextToSign validation failed: Text is empty or null.");
				return Error.Validation("Text.Empty", "Text is required for translation.");
			}

			try
			{
				using var content = new MultipartFormDataContent();

				content.Add(new StringContent(text), "text");
				content.Add(new StringContent(avatar ?? "default"), "avatar");
				content.Add(new StringContent(speed ?? "1.0"), "speed");

				var response = await _client.PostAsync("translate/to-sign", content);

				if (!response.IsSuccessStatusCode)
				{
					var body = await response.Content.ReadAsStringAsync();

					_logger.LogError("AI Server returned error in TextToSign. Status: {StatusCode}, Body: {Body}",
					(int)response.StatusCode,
					body);

					return Error.Failure("AiServer.Error", $"AI Server Error ({response.StatusCode}): {body}");
				}

				var result  = await response.Content.ReadAsStringAsync();
				_logger.LogInformation("TextToSign translation completed successfully.");
				return result;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Exception occurred during TextToSign connection.");
				return Error.Failure("AiServer.Connection", $"Failed to connect to AI Server: {ex.Message}");
			}
		}

		public async Task<Result<string>> SendAudioToSignAsync(IFormFile audio, string avatar, string speed)
		{
			_logger.LogInformation("Starting AudioToSign request. File: {FileName}, Size: {Size}, Avatar: {Avatar}, Speed: {Speed}",
				audio?.FileName, audio?.Length, avatar, speed);

			if (audio is null || audio.Length == 0)
			{
				_logger.LogWarning("AudioToSign validation failed: Audio file is null or empty.");
				return Error.Validation("Audio.Empty", "Audio file is required.");
			}

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
					var body = await response.Content.ReadAsStringAsync();

					_logger.LogError("AI Server returned error in AudioToSign. StatusCode: {StatusCode}, Body: {Body}",
						response.StatusCode, body);

					return Error.Failure("AiServer.Error", $"AI Server Error ({response.StatusCode}): {body}");
				}

				var resultString = await response.Content.ReadAsStringAsync();

				
				_logger.LogInformation("AudioToSign translation completed successfully for file: {FileName}", audio.FileName);

				return resultString;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Exception occurred during AudioToSign for file: {FileName}", audio.FileName);
				return Error.Failure("AiServer.Connection", $"Failed to connect to AI Server: {ex.Message}");
			}
		}
	}
}