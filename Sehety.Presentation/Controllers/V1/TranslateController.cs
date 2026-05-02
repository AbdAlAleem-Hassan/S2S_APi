using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Logging;
using S2S.ServicesAbstraction;
using S2S.Shared.CommonResult;
using S2S.Shared.DataTransferObjects.V1.TranslationDTOs;
using S2S.Shared.Security;
using System.Text.Json;

namespace S2S.Presentation.Controllers.V1
{
	[Authorize]	
	[ApiVersion("1.0")]
	[Route("api/v{version:apiVersion}/[controller]")]
	public class TranslateController(
		IAiTranslationService _service,
		ISpeechToTextService _speechToTextService,
		ITextToSpeechService _textToSpeechService,
		IWebHostEnvironment _env,
		ILogger<TranslateController> _logger) : ApiBaseController
	{
		private const long MaxVideoSizeBytes = 50L * 1024 * 1024;
		private const long MaxAudioSizeBytes = 20L * 1024 * 1024;

		private static readonly HashSet<string> AllowedVideoExtensions = new(StringComparer.OrdinalIgnoreCase)
		{
			".mp4", ".mov", ".webm", ".avi", ".mkv", ".m4v"
		};

		private static readonly HashSet<string> AllowedVideoContentTypes = new(StringComparer.OrdinalIgnoreCase)
		{
			"video/mp4", "video/quicktime", "video/webm", "video/x-msvideo", "video/x-matroska", "video/x-m4v"
		};

		private string? RewriteUrl(string fileName, string type)
		{
			if (string.IsNullOrEmpty(fileName)) return null;

			var request = HttpContext.Request;
			var baseUrl = $"{request.Scheme}://{request.Host}{request.PathBase}";
			return $"{baseUrl}/api/v1/media/{type}/{fileName}";
		}

		// 💡 الدالة السحرية دي بتاخد اللينك من الـ AI، تعرف نوعه، تحمله، وترجعلك اللينك الجديد!
		private async Task<string?> ProcessAndDownloadMediaAsync(string? originalUrl)
		{
			if (string.IsNullOrEmpty(originalUrl)) return null;

			// استخراج اسم الملف (مثلاً: 90bb8a83bdc94f318ebae77f44512871.pose)
			string fileName = Path.GetFileName(originalUrl);
			string type = "video"; // الافتراضي

			// تحديد نوع الفولدر بناءً على الامتداد
			if (fileName.EndsWith(".pose")) type = "pose";
			else if (fileName.EndsWith(".sigml")) type = "sigml";
			else if (fileName.EndsWith(".mp3") || fileName.EndsWith(".wav")) type = "audio";

			// تحميل وحفظ الملف من الـ AI Server إلى wwwroot
			var downloadResult = await _service.DownloadAndSaveMediaAsync(fileName, type);

			if (downloadResult.IsSuccess)
			{
				return RewriteUrl(fileName, type);
			}
			return null;
		}

		private static string? ExtractTranslationString(Dictionary<string, object?> translation, string key)
		{
			if (!translation.TryGetValue(key, out var value) || value == null)
			{
				return null;
			}

			return value switch
			{
				JsonElement { ValueKind: JsonValueKind.String } element => element.GetString(),
				string text => text,
				_ => null
			};
		}

		private static Result ValidateVideoFile(IFormFile video)
		{
			if (video == null || video.Length == 0)
			{
				return Error.Validation("Video.Empty", "Video file is required.");
			}

			if (video.Length > MaxVideoSizeBytes)
			{
				return Error.Validation("Video.TooLarge", "Video file exceeds 50 MB.");
			}

			var fileName = Path.GetFileName(video.FileName);
			var extension = Path.GetExtension(fileName);
			if (string.IsNullOrWhiteSpace(extension) || !AllowedVideoExtensions.Contains(extension))
			{
				return Error.Validation("Video.InvalidFormat", "Unsupported video format.");
			}

			if (!string.IsNullOrWhiteSpace(video.ContentType)
				&& !AllowedVideoContentTypes.Contains(video.ContentType))
			{
				return Error.Validation("Video.InvalidContentType", "Unsupported video content type.");
			}

			if (!FileSignatureValidator.IsAllowedVideo(video, extension))
			{
				return Error.Validation("Video.InvalidFormat", "Video signature does not match file type.");
			}

			return Result.Ok();
		}

		private static string? NormalizeTtsLanguage(string? language)
		{
			if (string.IsNullOrWhiteSpace(language))
			{
				return null;
			}

			return string.Equals(language, "ar", StringComparison.OrdinalIgnoreCase)
				? null
				: language;
		}

		[HttpPost("sign-to-text")]
		[Consumes("multipart/form-data")]
		[RequestSizeLimit(MaxVideoSizeBytes)]
		[ProducesResponseType<SignToTextResponseDTO>(StatusCodes.Status200OK)]
		[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
		[ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
		//[EndpointName("Convert Sign To Text")]
		[EndpointSummary("Send Sign and Return Text")]
		[EndpointDescription("Process The Sign Input Using AI Model And Convert Sign To Text")]
		public async Task<ActionResult<SignToTextResponseDTO>> SignToText([FromForm] SignToTextRequest request, CancellationToken cancellationToken)
		{
			var validationResult = ValidateVideoFile(request.VideoFile);
			if (validationResult.IsFailure)
			{
				return HandleRequest(Result<SignToTextResponseDTO>.Fail(validationResult.Errors.ToList()));
			}

			var serviceResult = await _service.SendSignToTextAsync(request.VideoFile);

			if (!serviceResult.IsSuccess) return HandleRequest(Result<SignToTextResponseDTO>.Fail(serviceResult.Errors.ToList()));

			try
			{
				var resultDto = JsonSerializer.Deserialize<SignToTextResponseDTO>(serviceResult.Value);
				if (resultDto == null)
				{
					return HandleRequest(Result<SignToTextResponseDTO>.Fail(
						Error.Failure("Translation.ParseError", "Invalid response from AI server.")));
				}

				if (request.IncludeAudio)
				{
					if (resultDto.translation == null)
					{
						_logger.LogWarning("Sign-to-text response missing translation payload. Audio generation skipped.");
					}
					else
					{
						var text = ExtractTranslationString(resultDto.translation, "text");
						if (string.IsNullOrWhiteSpace(text))
						{
							_logger.LogWarning("Sign-to-text response missing text. Audio generation skipped.");
						}
						else
						{
							var ttsLanguage = NormalizeTtsLanguage(request.Language);
							var ttsResult = await _textToSpeechService.SynthesizeAsync(text, ttsLanguage, cancellationToken);
							if (ttsResult.IsSuccess)
							{
								resultDto.translation["audio_url"] = RewriteUrl(ttsResult.Value, "audio");
							}
							else
							{
								var errorCode = ttsResult.Errors.FirstOrDefault()?.Code ?? "Tts.Failed";
								_logger.LogWarning("TTS failed for sign-to-text. Error: {ErrorCode}", errorCode);
							}
						}
					}
				}

				return HandleRequest(Result<SignToTextResponseDTO>.Ok(resultDto));
			}
			catch (Exception ex)
			{
				return BadRequest(new { error = "Translation Failed", details = ex.Message });
			}
		}

		[HttpPost("audio-to-text")]
		[Consumes("multipart/form-data")]
		[EnableRateLimiting("stt-limit")]
		[RequestSizeLimit(MaxAudioSizeBytes)]
		[ProducesResponseType<AudioToTextResponseDTO>(StatusCodes.Status200OK)]
		[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
		[ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
		[EndpointSummary("Send Audio and Return Text")]
		[EndpointDescription("Process audio using STT and return recognized text for review")]
		public async Task<ActionResult<AudioToTextResponseDTO>> AudioToText([FromForm] AudioToTextRequest request, CancellationToken cancellationToken)
		{
			var sttResult = await _speechToTextService.TranscribeAsync(request.AudioFile, request.Language, cancellationToken);
			if (!sttResult.IsSuccess)
			{
				return HandleRequest(Result<AudioToTextResponseDTO>.Fail(sttResult.Errors.ToList()));
			}

			return HandleRequest(Result<AudioToTextResponseDTO>.Ok(new AudioToTextResponseDTO
			{
				text = sttResult.Value
			}));
		}

		[HttpPost("text-to-sign")]
		[Consumes("multipart/form-data")]
		[ProducesResponseType<ToSignResponseDTO>(StatusCodes.Status200OK)]
		[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
		[ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
		//[EndpointName("Convert Text To Sign")]
		[EndpointSummary("Send Text and Return Sign")]
		[EndpointDescription("Process The Text Input Using AI Model And Convert Text To Avatar")]
		public async Task<ActionResult<ToSignResponseDTO>> TextToSign([FromForm] TextToSignRequest request)
		{
			var serviceResult = await _service.SendTextToSignAsync(request.Text, request.Avatar, request.Speed, request.OutputFormat);

			if (!serviceResult.IsSuccess) return HandleRequest(Result<ToSignResponseDTO>.Fail(serviceResult.Errors.ToList()));

			try
			{
				var resultDto = JsonSerializer.Deserialize<ToSignResponseDTO>(serviceResult.Value);
				if (resultDto == null)
				{
					return HandleRequest(Result<ToSignResponseDTO>.Fail(
						Error.Failure("Translation.ParseError", "Invalid response from AI server.")));
				}

				if (resultDto.translation != null)
				{
					// معالجة الفيديو لو موجود
					if (!string.IsNullOrEmpty(resultDto.translation.video_url))
						resultDto.translation.video_url = await ProcessAndDownloadMediaAsync(resultDto.translation.video_url);

					// معالجة الـ Pose لو موجود
					if (!string.IsNullOrEmpty(resultDto.translation.pose_url))
						resultDto.translation.pose_url = await ProcessAndDownloadMediaAsync(resultDto.translation.pose_url);

					// ملحوظة: الـ sigml_content بيرجع نص XML جاهز فمش محتاج تحميل، هيرجع للموبايل زي ما هو
				}

				return HandleRequest(Result<ToSignResponseDTO>.Ok(resultDto));
			}
			catch (Exception ex)
			{
				return StatusCode(500, new { error = "خطأ في تحويل البيانات", details = ex.Message });
			}
		}

		[HttpPost("audio-to-sign")]
		[Consumes("multipart/form-data")]
		[ProducesResponseType<ToSignResponseDTO>(StatusCodes.Status200OK)]
		[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
		[ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
		//[EndpointName("Convert Audio To Sign")]
		[EndpointSummary("Send Audio and Return Sign")]
		[EndpointDescription("Process The Audio Input Using AI Model And Convert Audio To Avatar")]
		[EnableRateLimiting("stt-limit")]
		[RequestSizeLimit(MaxAudioSizeBytes)]
		public async Task<ActionResult<ToSignResponseDTO>> AudioToSign([FromForm] AudioToSignRequest request, CancellationToken cancellationToken)
		{
			var sttResult = await _speechToTextService.TranscribeAsync(request.AudioFile, "ar", cancellationToken);
			if (!sttResult.IsSuccess)
			{
				return HandleRequest(Result<ToSignResponseDTO>.Fail(sttResult.Errors.ToList()));
			}

			var serviceResult = await _service.SendTextToSignAsync(sttResult.Value, request.Avatar, request.Speed, request.OutputFormat);
			if (!serviceResult.IsSuccess) return HandleRequest(Result<ToSignResponseDTO>.Fail(serviceResult.Errors.ToList()));

			try
			{
				var resultDto = JsonSerializer.Deserialize<ToSignResponseDTO>(serviceResult.Value);
				if (resultDto == null)
				{
					return HandleRequest(Result<ToSignResponseDTO>.Fail(
						Error.Failure("Translation.ParseError", "Invalid response from AI server.")));
				}

				if (resultDto.translation != null)
				{
					// 1. لو راجع فيديو عادي
					if (!string.IsNullOrEmpty(resultDto.translation.video_url))
					{
						resultDto.translation.video_url = await ProcessAndDownloadMediaAsync(resultDto.translation.video_url);
					}

					// 2. لو راجع Pose
					if (!string.IsNullOrEmpty(resultDto.translation.pose_url))
					{
						resultDto.translation.pose_url = await ProcessAndDownloadMediaAsync(resultDto.translation.pose_url);
					}

					// الـ Sigml بيعدي زي ما هو
				}

				return HandleRequest(Result<ToSignResponseDTO>.Ok(resultDto));
			}
			catch (Exception ex)
			{
				return StatusCode(500, new { error = ex.Message });
			}
		}

		[HttpGet("/api/v{version:apiVersion}/media/{type}/{fileName}")]
		[AllowAnonymous]
		public IActionResult GetMedia(string type, string fileName)
		{
			var webRootPath = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
			var filePath = Path.Combine(webRootPath, "media", type, fileName);

			if (!System.IO.File.Exists(filePath))
			{
				return NotFound(new { error = "File not found." });
			}

			var provider = new FileExtensionContentTypeProvider();
			provider.Mappings[".pose"] = "application/octet-stream";
			provider.Mappings[".sigml"] = "application/xml";

			if (!provider.TryGetContentType(filePath, out var contentType))
			{
				contentType = "application/octet-stream";
			}

			// 👈 التعديل هنا: ضفنا fileName كباراميتر تالت
			// ده بيجبر السيرفر يبعت Header بيقول للموبايل: "نزل الملف ده باسمه الأصلي"
			return PhysicalFile(filePath, contentType, fileName);
		}
	}
}