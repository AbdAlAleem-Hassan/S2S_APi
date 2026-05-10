using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using S2S.ServicesAbstraction;
using S2S.Shared.CommonResult;
using S2S.Shared.Constants;
using S2S.Shared.Security;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace S2S.Services
{
    public class GroqSpeechToTextService : ISpeechToTextService
    {
        private const string DefaultModel = "whisper-large-v3-turbo";
        private const string DefaultLanguage = "ar";
        private const string DefaultEndpoint = "https://api.groq.com/openai/v1/audio/transcriptions";

        /// <summary>
        /// If ALL segments have no_speech_prob above this threshold,
        /// the audio is considered silent / no speech detected.
        /// </summary>
        private const double NoSpeechThreshold = 0.85;

        /// <summary>
        /// Minimum transcription length to be considered valid speech.
        /// Very short results (1-2 chars) are likely noise artifacts.
        /// </summary>
        private const int MinTranscriptionLength = 2;

        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".mp3", ".wav", ".m4a", ".ogg", ".webm", ".mp4", ".mpeg"
        };

        private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "audio/mpeg", "audio/wav", "audio/x-wav", "audio/mp4", "audio/m4a",
            "audio/x-m4a", "audio/ogg", "audio/webm", "video/mp4", "video/webm"
        };

        private readonly HttpClient _client;
        private readonly IConfiguration _configuration;
        private readonly ILogger<GroqSpeechToTextService> _logger;
        private readonly GroqApiKeyPool _keyPool;
        private readonly int _timeoutSeconds;

        public GroqSpeechToTextService(
            HttpClient client,
            IConfiguration configuration,
            ILogger<GroqSpeechToTextService> logger,
            GroqApiKeyPool keyPool)
        {
            _client = client;
            _configuration = configuration;
            _logger = logger;
            _keyPool = keyPool;
            _timeoutSeconds = Math.Clamp(configuration.GetValue("SttSettings:TimeoutSeconds", 30), 5, 120);
        }

        public async Task<Result<string>> TranscribeAsync(IFormFile audio, string? language = null, CancellationToken cancellationToken = default)
        {
            // === VALIDATION ===
            var validationResult = ValidateAudioFile(audio);
            if (validationResult != null)
                return validationResult;

            // === GET API KEY (round-robin) ===
            var apiKey = _keyPool.GetNextKey();
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogError("All Groq API keys are exhausted or not configured.");
                return Error.Failure("Stt.AllKeysExhausted", "Speech-to-text service is temporarily unavailable. Please try again later.");
            }

            var endpoint = _configuration["Groq:Endpoint"] ?? DefaultEndpoint;
            var selectedLanguage = string.IsNullOrWhiteSpace(language) ? DefaultLanguage : language;

            // === SEND REQUEST (with retry on quota) ===
            return await SendWithRetry(audio, apiKey, endpoint, selectedLanguage, cancellationToken);
        }

        /// <summary>
        /// Validates the audio file (size, extension, content type, magic bytes).
        /// Returns an Error if invalid, or null if valid.
        /// </summary>
        private Result<string>? ValidateAudioFile(IFormFile audio)
        {
            if (audio is null || audio.Length == 0)
                return Error.Validation("Audio.Empty", "Audio file is required.");

            if (audio.Length > MediaDefaults.MaxAudioSizeBytes)
                return Error.Validation("Audio.TooLarge", $"Audio file exceeds {MediaDefaults.MaxAudioSizeBytes / (1024 * 1024)} MB.");

            var extension = Path.GetExtension(audio.FileName);
            if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
                return Error.Validation("Audio.InvalidFormat", "Unsupported audio format. Allowed: mp3, wav, m4a, ogg, webm, mp4.");

            if (!string.IsNullOrWhiteSpace(audio.ContentType) && !AllowedContentTypes.Contains(audio.ContentType))
                return Error.Validation("Audio.InvalidContentType", "Unsupported audio content type.");

            if (!FileSignatureValidator.IsAllowedAudio(audio, extension))
                return Error.Validation("Audio.InvalidFormat", "File content does not match the expected audio format.");

            return null; // Valid
        }

        /// <summary>
        /// Sends the request with automatic retry on 429 (quota exhausted).
        /// Tries the next available API key if the first one is rate-limited.
        /// </summary>
        private async Task<Result<string>> SendWithRetry(
            IFormFile audio, string apiKey, string endpoint, string language, CancellationToken cancellationToken)
        {
            var httpResponse = await SendRequest(audio, apiKey, endpoint, language, cancellationToken);
            if (httpResponse == null)
                return Error.Failure("Stt.Connection", "Failed to connect to speech-to-text service. Please try again.");

            // Handle quota (429) — retry with next key
            if (httpResponse.StatusCode == HttpStatusCode.TooManyRequests)
            {
                _keyPool.MarkQuotaExhausted(apiKey);
                httpResponse.Dispose();

                var retryKey = _keyPool.GetNextKey();
                if (string.IsNullOrWhiteSpace(retryKey))
                {
                    _logger.LogWarning("All Groq API keys quota-exhausted. No retry possible.");
                    return Error.Failure("Stt.QuotaExhausted", "Speech-to-text quota exceeded. Please try again later.");
                }

                _logger.LogInformation("Retrying STT with next API key after 429.");
                httpResponse = await SendRequest(audio, retryKey, endpoint, language, cancellationToken);
                if (httpResponse == null)
                    return Error.Failure("Stt.Connection", "Failed to connect to speech-to-text service. Please try again.");

                if (httpResponse.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    _keyPool.MarkQuotaExhausted(retryKey);
                    httpResponse.Dispose();
                    return Error.Failure("Stt.QuotaExhausted", "Speech-to-text quota exceeded. Please try again later.");
                }
            }

            // Handle other errors
            if (!httpResponse.IsSuccessStatusCode)
            {
                var errorBody = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("STT provider error. Status: {StatusCode}, Body: {Body}",
                    (int)httpResponse.StatusCode, Truncate(errorBody, 512));
                httpResponse.Dispose();
                return Error.Failure("Stt.ProviderError", "Speech-to-text service encountered an error. Please try again.");
            }

            return await ParseTranscriptionResponse(httpResponse, cancellationToken);
        }

        /// <summary>
        /// Sends the transcription request to Groq API.
        /// Uses verbose_json format to get no_speech_prob for silence detection.
        /// Returns null on connection/timeout failure.
        /// </summary>
        private async Task<HttpResponseMessage?> SendRequest(
            IFormFile audio, string apiKey, string endpoint, string language, CancellationToken cancellationToken)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                var form = new MultipartFormDataContent();
                form.Add(new StringContent(DefaultModel), "model");
                form.Add(new StringContent(language), "language");
                // verbose_json returns segments with no_speech_prob for silence detection
                form.Add(new StringContent("verbose_json"), "response_format");

                var stream = audio.OpenReadStream();
                var fileContent = new StreamContent(stream);
                if (!string.IsNullOrWhiteSpace(audio.ContentType))
                {
                    fileContent.Headers.ContentType = new MediaTypeHeaderValue(audio.ContentType);
                }
                form.Add(fileContent, "file", Path.GetFileName(audio.FileName));

                request.Content = form;

                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(_timeoutSeconds));

                return await _client.SendAsync(request, HttpCompletionOption.ResponseContentRead, timeoutCts.Token);
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("STT request timed out after {TimeoutSeconds} seconds.", _timeoutSeconds);
                return null;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "STT connection failed.");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during STT request.");
                return null;
            }
        }

        /// <summary>
        /// Parses the verbose_json response from Groq/Whisper.
        /// Detects silent audio by checking no_speech_prob on each segment.
        /// </summary>
        private async Task<Result<string>> ParseTranscriptionResponse(HttpResponseMessage httpResponse, CancellationToken cancellationToken)
        {
            try
            {
                var json = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
                httpResponse.Dispose();

                if (string.IsNullOrWhiteSpace(json))
                    return Error.Failure("Stt.EmptyResponse", "Speech recognition returned an empty response.");

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // Extract the full transcription text
                var text = root.TryGetProperty("text", out var textProp) ? textProp.GetString()?.Trim() : null;

                // Check for silent/empty audio using segment-level no_speech_prob
                if (root.TryGetProperty("segments", out var segments) && segments.ValueKind == JsonValueKind.Array)
                {
                    var segmentCount = segments.GetArrayLength();

                    if (segmentCount == 0)
                    {
                        _logger.LogInformation("STT returned 0 segments — no speech detected.");
                        return Error.Validation("Audio.NoSpeech", "No speech detected in the audio. Please record again with clear speech.");
                    }

                    // Check if ALL segments have high no_speech_prob
                    var allSilent = true;
                    foreach (var segment in segments.EnumerateArray())
                    {
                        if (segment.TryGetProperty("no_speech_prob", out var nsp))
                        {
                            if (nsp.GetDouble() < NoSpeechThreshold)
                            {
                                allSilent = false;
                                break;
                            }
                        }
                        else
                        {
                            // If no_speech_prob is missing, assume it has speech
                            allSilent = false;
                            break;
                        }
                    }

                    if (allSilent)
                    {
                        _logger.LogInformation("STT detected silent audio (all segments no_speech_prob > {Threshold}).", NoSpeechThreshold);
                        return Error.Validation("Audio.NoSpeech", "No speech detected in the audio. Please record again with clear speech.");
                    }
                }

                // Final validation on the transcription text
                if (string.IsNullOrWhiteSpace(text) || text.Length < MinTranscriptionLength)
                {
                    _logger.LogInformation("STT returned empty or too-short text: '{Text}'", text ?? "(null)");
                    return Error.Validation("Audio.NoSpeech", "No clear speech detected. Please speak louder and try again.");
                }

                return text;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse STT response JSON.");
                return Error.Failure("Stt.ParseError", "Failed to process speech recognition result.");
            }
        }

        private static string Truncate(string? value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
                return value ?? string.Empty;

            return value[..maxLength];
        }
    }
}
