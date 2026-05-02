using Google.Cloud.TextToSpeech.V1;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using S2S.ServicesAbstraction;
using S2S.Shared.CommonResult;
using System.Text.Json;

namespace S2S.Services
{
	public class GoogleTextToSpeechService : ITextToSpeechService
	{
		private const int MaxTextLength = 2000;
		private const string DefaultLanguageCode = "ar-XA";
		private const string DefaultVoiceName = "ar-XA-Wavenet-D";

		private readonly TextToSpeechClient _client;
		private readonly IWebHostEnvironment _env;
		private readonly ILogger<GoogleTextToSpeechService> _logger;
		private readonly string _languageCode;
		private readonly string _voiceName;

		public GoogleTextToSpeechService(IWebHostEnvironment env, IConfiguration configuration, ILogger<GoogleTextToSpeechService> logger)
		{
			_env = env;
			_logger = logger;
			_languageCode = configuration["TtsSettings:LanguageCode"] ?? DefaultLanguageCode;
			_voiceName = configuration["TtsSettings:VoiceName"] ?? DefaultVoiceName;
			var builder = new TextToSpeechClientBuilder();
			var credentialsJson = configuration["Google:CredentialsJson"];
			if (string.IsNullOrWhiteSpace(credentialsJson))
			{
				credentialsJson = BuildCredentialsJson(configuration.GetSection("Google:Credentials"));
			}

			if (!string.IsNullOrWhiteSpace(credentialsJson))
			{
				builder.JsonCredentials = credentialsJson;
			}
			else
			{
				var credentialsPath = configuration["Google:ApplicationCredentials"];
				if (!string.IsNullOrWhiteSpace(credentialsPath))
				{
					builder.CredentialsPath = credentialsPath;
				}
			}

			_client = builder.Build();
		}

		public async Task<Result<string>> SynthesizeAsync(string text, string? languageCode = null, CancellationToken cancellationToken = default)
		{
			if (string.IsNullOrWhiteSpace(text))
			{
				return Error.Validation("Tts.EmptyText", "Text is required for speech synthesis.");
			}

			if (text.Length > MaxTextLength)
			{
				return Error.Validation("Tts.TextTooLong", "Text exceeds the maximum length for speech synthesis.");
			}

			var voiceLanguage = string.IsNullOrWhiteSpace(languageCode) ? _languageCode : languageCode;

			var request = new SynthesizeSpeechRequest
			{
				Input = new SynthesisInput { Text = text },
				Voice = new VoiceSelectionParams
				{
					LanguageCode = voiceLanguage,
					Name = _voiceName,
					SsmlGender = SsmlVoiceGender.Female
				},
				AudioConfig = new AudioConfig
				{
					AudioEncoding = AudioEncoding.Mp3,
					SpeakingRate = 1.0,
					Pitch = 0.0
				}
			};

			SynthesizeSpeechResponse response;
			try
			{
				response = await _client.SynthesizeSpeechAsync(request, cancellationToken);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "TTS synthesis failed.");
				return Error.Failure("Tts.Failed", "Text-to-speech synthesis failed.");
			}

			var audioBytes = response.AudioContent.ToByteArray();
			if (audioBytes.Length == 0)
			{
				return Error.Failure("Tts.EmptyAudio", "Text-to-speech returned empty audio.");
			}

			var webRootPath = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
			var audioFolder = Path.Combine(webRootPath, "media", "audio");
			Directory.CreateDirectory(audioFolder);

			var fileName = $"{Guid.NewGuid():N}.mp3";
			var filePath = Path.Combine(audioFolder, fileName);

			await File.WriteAllBytesAsync(filePath, audioBytes, cancellationToken);

			return fileName;
		}

		private static string? BuildCredentialsJson(IConfigurationSection section)
		{
			if (!section.Exists())
			{
				return null;
			}

			var data = section.GetChildren()
				.ToDictionary(child => child.Key, child => child.Value ?? string.Empty);

			if (data.TryGetValue("private_key", out var privateKey)
				&& !string.IsNullOrWhiteSpace(privateKey)
				&& privateKey.Contains("\\n", StringComparison.Ordinal))
			{
				data["private_key"] = privateKey.Replace("\\n", "\n", StringComparison.Ordinal);
			}

			if (data.Count == 0)
			{
				return null;
			}

			return JsonSerializer.Serialize(data);
		}
	}
}
