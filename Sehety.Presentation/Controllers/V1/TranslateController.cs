using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using S2S.ServicesAbstraction;
using S2S.Shared.CommonResult;
using S2S.Shared.Constants;
using S2S.Shared.DataTransferObjects.V1.TranslationDTOs;
using S2S.Shared.Helpers;
using S2S.Shared.Security;
using System.Security.Claims;
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
		IConfiguration _configuration,
		ITranslationHistoryService _historyService,
	ILogger<TranslateController> _logger) : ApiBaseController
	{
		private const long MaxVideoSizeBytes = MediaDefaults.MaxVideoSizeBytes;
		private const long MaxAudioSizeBytes = MediaDefaults.MaxAudioSizeBytes;

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
			return UrlRewriter.BuildMediaUrl(HttpContext, fileName, type);
		}

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
		private async Task<string?> SaveUploadedVideoAsync(IFormFile video)
		{
			if (video == null || video.Length == 0) return null;
			var fileName = $"{Guid.NewGuid()}{Path.GetExtension(video.FileName)}";
			var webRootPath = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
			var uploadsFolder = Path.Combine(webRootPath, "media", "video");

			if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);
			var filePath = Path.Combine(uploadsFolder, fileName);

			using (var stream = new FileStream(filePath, FileMode.Create))
			{
				await video.CopyToAsync(stream);
			}
			return RewriteUrl(fileName, "video");
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
		[EnableRateLimiting("stt-limit")]
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

			// 👇 التعديل هنا: استقبال الـ ID كـ string مباشرة
			// هيدور على الـ ID في أشهر الأسماء اللي بنستخدمها في الـ JWT
			var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
					  ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)
					  ?? User.FindFirstValue("uid")
					  ?? User.FindFirstValue("id");

			if (string.IsNullOrEmpty(userId))
			{
				// لو لسه مش لاقيه، السطر ده هيطبعلك كل الـ Claims اللي في التوكن في الكونسول عشان تعرف اسمها إيه بالظبط
				var claims = string.Join(", ", User.Claims.Select(c => c.Type));
				_logger.LogWarning("User ID not found in token. Available claims: {Claims}", claims);

				return Unauthorized(new { message = "Invalid token claims. Could not extract User ID." });
			}

			var serviceResult = await _service.SendSignToTextAsync(request.VideoFile);
			if (!serviceResult.IsSuccess) return HandleRequest(Result<SignToTextResponseDTO>.Fail(serviceResult.Errors.ToList()));

			try
			{
				var resultDto = JsonSerializer.Deserialize<SignToTextResponseDTO>(serviceResult.Value);
				string? generatedAudioUrl = null;
				string? textResult = null;

				if (resultDto?.translation != null && resultDto.translation.TryGetValue("text", out var txtObj))
				{
					textResult = txtObj?.ToString();
				}

				if (request.IncludeAudio && !string.IsNullOrWhiteSpace(textResult))
				{
					var ttsResult = await _textToSpeechService.SynthesizeAsync(textResult, request.Language, cancellationToken);
					if (ttsResult.IsSuccess)
					{
						generatedAudioUrl = RewriteUrl(ttsResult.Value, "audio");
						resultDto.translation["audio_url"] = generatedAudioUrl;
					}
				}

				if (request.SaveToHistory && !string.IsNullOrWhiteSpace(textResult))
				{
					var uploadedVideoUrl = await SaveUploadedVideoAsync(request.VideoFile);

					// تمرير userId كـ string
					await _historyService.SaveSignToTextHistoryAsync(userId, uploadedVideoUrl, textResult, generatedAudioUrl);
				}

				return HandleRequest(Result<SignToTextResponseDTO>.Ok(resultDto));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Processing failed.");
				return StatusCode(500, new { error = "Processing failed." });
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
		[EnableRateLimiting("stt-limit")]
		[Consumes("multipart/form-data")]
		[ProducesResponseType<ToSignResponseDTO>(StatusCodes.Status200OK)]
		[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
		[ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
		//[EndpointName("Convert Text To Sign")]
		[EndpointSummary("Send Text and Return Sign")]
		[EndpointDescription("Process The Text Input Using AI Model And Convert Text To Avatar")]
		public async Task<ActionResult<ToSignResponseDTO>> TextToSign([FromForm] TextToSignRequest request)
		{
			// 👇 التعديل هنا: استقبال الـ ID كـ string مباشرة
			// هيدور على الـ ID في أشهر الأسماء اللي بنستخدمها في الـ JWT
			var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
					  ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)
					  ?? User.FindFirstValue("uid")
					  ?? User.FindFirstValue("id");

			if (string.IsNullOrEmpty(userId))
			{
				// لو لسه مش لاقيه، السطر ده هيطبعلك كل الـ Claims اللي في التوكن في الكونسول عشان تعرف اسمها إيه بالظبط
				var claims = string.Join(", ", User.Claims.Select(c => c.Type));
				_logger.LogWarning("User ID not found in token. Available claims: {Claims}", claims);

				return Unauthorized(new { message = "Invalid token claims. Could not extract User ID." });
			}

			var serviceResult = await _service.SendTextToSignAsync(request.Text, request.Avatar, request.Speed, request.OutputFormat);
			if (!serviceResult.IsSuccess) return HandleRequest(Result<ToSignResponseDTO>.Fail(serviceResult.Errors.ToList()));

			try
			{
				var resultDto = JsonSerializer.Deserialize<ToSignResponseDTO>(serviceResult.Value);
				if (resultDto?.translation != null)
				{
					if (!string.IsNullOrEmpty(resultDto.translation.video_url))
						resultDto.translation.video_url = await ProcessAndDownloadMediaAsync(resultDto.translation.video_url);

					if (!string.IsNullOrEmpty(resultDto.translation.pose_url))
						resultDto.translation.pose_url = await ProcessAndDownloadMediaAsync(resultDto.translation.pose_url);

					if (request.SaveToHistory)
					{
						// تمرير userId كـ string
						await _historyService.SaveTextToSignHistoryAsync(
							userId,
							request.Text,
							resultDto.translation.video_url,
							resultDto.translation.pose_url,
							resultDto.translation.sigml_content);
					}
				}
				return HandleRequest(Result<ToSignResponseDTO>.Ok(resultDto));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Processing failed.");
				return StatusCode(500, new { error = "Processing failed." });
			}
		}



		[HttpGet("history")]
		[ProducesResponseType<List<TranslationHistoryResponseDTO>>(StatusCodes.Status200OK)]
		[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
		[ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
		[EndpointSummary("Get User Translation History")]
		[EndpointDescription("Retrieve a paginated list of the user's past translations, ordered from newest to oldest.")]
		public async Task<ActionResult<List<TranslationHistoryResponseDTO>>> GetHistory([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
		{
			// جلب الـ ID بأمان كـ string ودعم كافة صيغ الـ Claims المحتملة في التوكن
			var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
					  ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)
					  ?? User.FindFirstValue("uid")
					  ?? User.FindFirstValue("id");

			if (string.IsNullOrEmpty(userId))
			{
				return Unauthorized(new { message = "Invalid token or user not authenticated." });
			}

			var result = await _historyService.GetUserHistoryAsync(userId, pageNumber, pageSize);

			if (!result.IsSuccess)
				return HandleRequest(Result<List<TranslationHistoryResponseDTO>>.Fail(result.Errors.ToList()));

			return HandleRequest(Result<List<TranslationHistoryResponseDTO>>.Ok(result.Value));
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
				_logger.LogError(ex, "Failed to parse audio-to-sign response.");
				return StatusCode(500, new { error = "Translation processing failed. Please try again." });
			}
		}

		[HttpGet("/api/v{version:apiVersion}/media/{type}/{fileName}")]
		[AllowAnonymous]
		[EnableRateLimiting(RateLimitPolicies.MediaLimit)]
		public IActionResult GetMedia(string type, string fileName)
		{
			// --- Sanitize filename: strip path components, reject traversal characters ---
			var safeName = Path.GetFileName(fileName);
			if (string.IsNullOrWhiteSpace(safeName)
				|| safeName.Contains("..", StringComparison.Ordinal)
				|| safeName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
			{
				return BadRequest(new { error = "Invalid file name." });
			}

			// --- Sanitize type: only allow known media types ---
			var safeType = Path.GetFileName(type);
			var allowedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
				{ "audio", "video", "pose", "profile" };
			if (!allowedTypes.Contains(safeType))
			{
				return BadRequest(new { error = "Invalid media type." });
			}

			// --- Resolve base directory: profile uses external storage, others use wwwroot ---
			string baseDir;
			if (string.Equals(safeType, "profile", StringComparison.OrdinalIgnoreCase))
			{
				var uploadBase = _configuration["UploadStorage:BasePath"] ?? "/var/www/uploads";
				baseDir = Path.Combine(uploadBase, "profile");
			}
			else
			{
				var webRootPath = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
				baseDir = Path.Combine(webRootPath, "media", safeType);
			}

			var filePath = Path.GetFullPath(Path.Combine(baseDir, safeName));
			var resolvedBaseDir = Path.GetFullPath(baseDir);

			// --- Path traversal guard: resolved path must stay inside base directory ---
			if (!filePath.StartsWith(resolvedBaseDir, StringComparison.OrdinalIgnoreCase))
			{
				return BadRequest(new { error = "Invalid file name." });
			}

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

			// Profile images: GUID filenames are immutable — safe to cache long-term
			// Other media (audio/video): shorter cache since URLs may be reused
			if (string.Equals(safeType, "profile", StringComparison.OrdinalIgnoreCase))
			{
				Response.Headers["Cache-Control"] = "public, max-age=604800, immutable"; // 7 days
				return PhysicalFile(filePath, contentType);
			}

			Response.Headers["Cache-Control"] = "public, max-age=3600"; // 1 hour

			// Audio & Video: play inline in browser (no download prompt)
			// Pose & other files: force download since they're not browser-playable
			if (string.Equals(safeType, "audio", StringComparison.OrdinalIgnoreCase) ||
				string.Equals(safeType, "video", StringComparison.OrdinalIgnoreCase))
			{
				return PhysicalFile(filePath, contentType);
			}

			return PhysicalFile(filePath, contentType, safeName);
		}
	}
}