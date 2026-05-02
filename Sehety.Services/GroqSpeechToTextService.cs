using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using S2S.ServicesAbstraction;
using S2S.Shared.CommonResult;
using S2S.Shared.Security;
using System.Net.Http.Headers;

namespace S2S.Services
{
	public class GroqSpeechToTextService : ISpeechToTextService
	{
		private const long MaxAudioSizeBytes = 20 * 1024 * 1024;
		private const string DefaultModel = "whisper-large-v3";
		private const string DefaultLanguage = "ar";
		private const string DefaultEndpoint = "https://api.groq.com/openai/v1/audio/transcriptions";

		private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
		{
			".mp3", ".wav", ".m4a", ".ogg", ".webm", ".mp4", ".mpeg"
		};

		private readonly HttpClient _client;
		private readonly IConfiguration _configuration;
		private readonly ILogger<GroqSpeechToTextService> _logger;
		private readonly int _timeoutSeconds;

		public GroqSpeechToTextService(HttpClient client, IConfiguration configuration, ILogger<GroqSpeechToTextService> logger)
		{
			_client = client;
			_configuration = configuration;
			_logger = logger;
			_timeoutSeconds = Math.Clamp(configuration.GetValue("SttSettings:TimeoutSeconds", 30), 5, 120);
		}

		public async Task<Result<string>> TranscribeAsync(IFormFile audio, string? language = null, CancellationToken cancellationToken = default)
		{
			if (audio is null || audio.Length == 0)
			{
				return Error.Validation("Audio.Empty", "Audio file is required.");
			}

			if (audio.Length > MaxAudioSizeBytes)
			{
				return Error.Validation("Audio.TooLarge", "Audio file exceeds 20 MB.");
			}

			var extension = Path.GetExtension(audio.FileName);
			if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
			{
				return Error.Validation("Audio.InvalidFormat", "Unsupported audio format.");
			}

			if (!FileSignatureValidator.IsAllowedAudio(audio, extension))
			{
				return Error.Validation("Audio.InvalidFormat", "Unsupported audio format.");
			}

			var apiKey = _configuration["GROQ_API_KEY"] ?? _configuration["Groq:ApiKey"];
			if (string.IsNullOrWhiteSpace(apiKey))
			{
				_logger.LogError("GROQ_API_KEY is missing for STT service.");
				return Error.Failure("Stt.ConfigMissing", "Speech-to-text service is not configured.");
			}

			var endpoint = _configuration["Groq:Endpoint"] ?? DefaultEndpoint;
			var selectedLanguage = string.IsNullOrWhiteSpace(language) ? DefaultLanguage : language;

			using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
			request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

			using var form = new MultipartFormDataContent();
			form.Add(new StringContent(DefaultModel), "model");
			form.Add(new StringContent(selectedLanguage), "language");
			form.Add(new StringContent("text"), "response_format");

			var fileContent = new StreamContent(audio.OpenReadStream());
			if (!string.IsNullOrWhiteSpace(audio.ContentType))
			{
				fileContent.Headers.ContentType = new MediaTypeHeaderValue(audio.ContentType);
			}
			form.Add(fileContent, "file", Path.GetFileName(audio.FileName));

			request.Content = form;

			HttpResponseMessage response;
			using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			timeoutCts.CancelAfter(TimeSpan.FromSeconds(_timeoutSeconds));
			try
			{
				response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token);
			}
			catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
			{
				_logger.LogWarning("STT request timed out after {TimeoutSeconds} seconds.", _timeoutSeconds);
				return Error.Failure("Stt.Timeout", "Speech-to-text request timed out.");
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "STT request failed for file: {FileName}", audio.FileName);
				return Error.Failure("Stt.Connection", "Failed to connect to STT provider.");
			}

			if (!response.IsSuccessStatusCode)
			{
				var body = await response.Content.ReadAsStringAsync(timeoutCts.Token);
				_logger.LogError("STT provider error. Status: {StatusCode}, Body: {Body}",
					(int)response.StatusCode,
					Truncate(body, 512));
				return Error.Failure("Stt.ProviderError", "Speech-to-text provider error.");
			}

			var text = (await response.Content.ReadAsStringAsync(timeoutCts.Token)).Trim();
			if (string.IsNullOrWhiteSpace(text))
			{
				return Error.Failure("Stt.EmptyResult", "Speech recognition returned empty text.");
			}

			return text;
		}

		private static string Truncate(string? value, int maxLength)
		{
			if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
			{
				return value ?? string.Empty;
			}

			return value[..maxLength];
		}
	}
}
