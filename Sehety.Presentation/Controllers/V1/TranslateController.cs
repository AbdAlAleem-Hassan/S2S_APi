using Microsoft.AspNetCore.Mvc;
using S2S.ServicesAbstraction;
using S2S.Shared.CommonResult;
using S2S.Shared.DataTransferObjects.V1.TranslationDTOs;
using System.Text.Json;

namespace S2S.Presentation.Controllers.V1
{
	[ApiVersion("1.0")]
	[Route("api/[controller]")]
	[Route("api/v{version:apiVersion}/[controller]")]
	public class TranslateController(IAiTranslationService _service) : ApiBaseController
	{
		private string RewriteUrl(string aiUrl, string type)
		{
			if (string.IsNullOrEmpty(aiUrl)) return null;

			var fileName = Path.GetFileName(aiUrl);
			var request = HttpContext.Request;
			var baseUrl = $"{request.Scheme}://{request.Host}{request.PathBase}";

			return $"{baseUrl}/api/media/{type}/{fileName}";
		}

		[HttpPost("sign-to-text")]
		[Consumes("multipart/form-data")]
		public async Task<ActionResult<SignToTextResponseDTO>> SignToText([FromForm] SignToTextRequest request)
		{
			var serviceResult = await _service.SendSignToTextAsync(request.VideoFile, request.Language, request.IncludeAudio);

			if (!serviceResult.IsSuccess)
			{
				return HandleRequest(Result<SignToTextResponseDTO>.Fail(serviceResult.Errors.ToList()));
			}

			try
			{
				var jsonString = serviceResult.Value;
				var resultDto = JsonSerializer.Deserialize<SignToTextResponseDTO>(jsonString);

				if (resultDto?.translation != null && resultDto.translation.ContainsKey("audio_url"))
				{
					var audioJsonElement = (JsonElement)resultDto.translation["audio_url"];
					if (audioJsonElement.ValueKind == JsonValueKind.String)
					{
						string originalUrl = audioJsonElement.GetString();
						resultDto.translation["audio_url"] = RewriteUrl(originalUrl, "audio");
					}
				}

				return HandleRequest(Result<SignToTextResponseDTO>.Ok(resultDto));
			}
			catch (Exception ex)
			{
				return BadRequest(new { error = "Translation Failed", details = ex.Message });
			}
		}

		[HttpPost("text-to-sign")]
		[Consumes("multipart/form-data")]
		public async Task<ActionResult<ToSignResponseDTO>> TextToSign([FromForm] TextToSignRequest request)
		{
			var serviceResult = await _service.SendTextToSignAsync(request.Text, request.Avatar, request.Speed);

			if (!serviceResult.IsSuccess)
			{
				return HandleRequest(Result<ToSignResponseDTO>.Fail(serviceResult.Errors.ToList()));
			}

			try
			{
				var jsonString = serviceResult.Value;
				var resultDto = JsonSerializer.Deserialize<ToSignResponseDTO>(jsonString);

				if (resultDto?.translation?.video_url != null)
				{
					resultDto.translation.video_url = RewriteUrl(resultDto.translation.video_url, "video");
				}

				return HandleRequest(Result<ToSignResponseDTO>.Ok(resultDto));
			}
			catch (Exception ex)
			{
				return StatusCode(500, new { error = ex.Message });
			}
		}

		[HttpPost("audio-to-sign")]
		[Consumes("multipart/form-data")]
		public async Task<ActionResult<ToSignResponseDTO>> AudioToSign([FromForm] AudioToSignRequest request)
		{
			var serviceResult = await _service.SendAudioToSignAsync(request.AudioFile, request.Avatar, request.Speed);

			if (!serviceResult.IsSuccess)
			{
				return HandleRequest(Result<ToSignResponseDTO>.Fail(serviceResult.Errors.ToList()));
			}

			try
			{
				var jsonString = serviceResult.Value;
				var resultDto = JsonSerializer.Deserialize<ToSignResponseDTO>(jsonString);

				if (resultDto?.translation?.video_url != null)
				{
					resultDto.translation.video_url = RewriteUrl(resultDto.translation.video_url, "video");
				}

				return HandleRequest(Result<ToSignResponseDTO>.Ok(resultDto));
			}
			catch (Exception ex)
			{
				return StatusCode(500, new { error = ex.Message });
			}
		}
	}
}