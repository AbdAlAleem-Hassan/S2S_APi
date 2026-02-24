using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using S2S.ServicesAbstraction;
using S2S.Shared.CommonResult;
using S2S.Shared.DataTransferObjects.V1.TranslationDTOs;
using System.Text.Json;

namespace S2S.Presentation.Controllers.V1
{
	[Authorize]
	[ApiVersion("1.0")]
	[Route("api/v{version:apiVersion}/[controller]")]
	public class TranslateController(IAiTranslationService _service) : ApiBaseController
	{
		private string RewriteUrl(string aiUrl, string type)
		{
			if (string.IsNullOrEmpty(aiUrl)) return null;

			var fileName = Path.GetFileName(aiUrl);
			var request = HttpContext.Request;
			var baseUrl = $"{request.Scheme}://{request.Host}{request.PathBase}";

			// التعديل هنا: شيلنا /api/ عشان يقرأ من فولدر wwwroot مباشرة
			return $"{baseUrl}/media/{type}/{fileName}";
		}

		[HttpPost("sign-to-text")]
		[Consumes("multipart/form-data")]
		[ProducesResponseType<SignToTextResponseDTO>(StatusCodes.Status200OK)]
		[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
		[ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
		//[EndpointName("Convert Sign To Text")]
		[EndpointSummary("Send Sign and Return Text")]
		[EndpointDescription("Process The Sign Input Using AI Model And Convert Sign To Text")]
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
						string fileName = Path.GetFileName(originalUrl);

						// 👈 هنا بنحمل الملف ونحفظه
						var downloadResult = await _service.DownloadAndSaveMediaAsync(fileName, "audio");

						if (downloadResult.IsSuccess)
						{
							resultDto.translation["audio_url"] = RewriteUrl(fileName, "audio");
						}
						else
						{
							resultDto.translation["audio_url"] = null;
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
		[ProducesResponseType<ToSignResponseDTO>(StatusCodes.Status200OK)]
		[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
		[ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
		//[EndpointName("Convert Audio To Sign")]
		[EndpointSummary("Send Audio and Return Sign")]
		[EndpointDescription("Process The Audio Input Using AI Model And Convert Audio To Avatar")]
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