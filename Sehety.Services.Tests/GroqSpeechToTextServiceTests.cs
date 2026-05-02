using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using S2S.Services;
using System.Net;
using System.Net.Http;
using Xunit;

namespace S2S.Services.Tests
{
	public class GroqSpeechToTextServiceTests
	{
		[Fact]
		public async Task TranscribeAsync_ReturnsValidation_WhenFileEmpty()
		{
			var service = CreateService(new Dictionary<string, string?> { ["GROQ_API_KEY"] = "test" });
			var file = CreateFormFile(Array.Empty<byte>(), "audio.mp3");

			var result = await service.TranscribeAsync(file);

			Assert.True(result.IsFailure);
			Assert.Equal("Audio.Empty", result.Errors[0].Code);
		}

		[Fact]
		public async Task TranscribeAsync_ReturnsValidation_WhenFormatInvalid()
		{
			var service = CreateService(new Dictionary<string, string?> { ["GROQ_API_KEY"] = "test" });
			var file = CreateFormFile(new byte[] { 1, 2, 3 }, "audio.txt");

			var result = await service.TranscribeAsync(file);

			Assert.True(result.IsFailure);
			Assert.Equal("Audio.InvalidFormat", result.Errors[0].Code);
		}

		[Fact]
		public async Task TranscribeAsync_ReturnsValidation_WhenSignatureInvalid()
		{
			var service = CreateService(new Dictionary<string, string?> { ["GROQ_API_KEY"] = "test" });
			var file = CreateFormFile(new byte[] { 0x00, 0x00, 0x00, 0x00 }, "audio.mp3");

			var result = await service.TranscribeAsync(file);

			Assert.True(result.IsFailure);
			Assert.Equal("Audio.InvalidFormat", result.Errors[0].Code);
		}

		[Fact]
		public async Task TranscribeAsync_ReturnsValidation_WhenFileTooLarge()
		{
			var service = CreateService(new Dictionary<string, string?> { ["GROQ_API_KEY"] = "test" });
			var data = new byte[20 * 1024 * 1024 + 1];
			var file = CreateFormFile(data, "audio.mp3");

			var result = await service.TranscribeAsync(file);

			Assert.True(result.IsFailure);
			Assert.Equal("Audio.TooLarge", result.Errors[0].Code);
		}

		[Fact]
		public async Task TranscribeAsync_ReturnsFailure_WhenApiKeyMissing()
		{
			var service = CreateService(new Dictionary<string, string?>());
			var file = CreateFormFile(new byte[] { 0x49, 0x44, 0x33, 0x03, 0x00, 0x00 }, "audio.mp3");

			var result = await service.TranscribeAsync(file);

			Assert.True(result.IsFailure);
			Assert.Equal("Stt.ConfigMissing", result.Errors[0].Code);
		}

		[Fact]
		public async Task TranscribeAsync_ReturnsText_WhenProviderSucceeds()
		{
			var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
			{
				Content = new StringContent("  hello ")
			});
			var service = CreateService(new Dictionary<string, string?> { ["GROQ_API_KEY"] = "test" }, handler);
			var file = CreateFormFile(new byte[] { 0x49, 0x44, 0x33, 0x03, 0x00, 0x00 }, "audio.mp3");

			var result = await service.TranscribeAsync(file);

			Assert.True(result.IsSuccess);
			Assert.Equal("hello", result.Value);
		}

		private static GroqSpeechToTextService CreateService(
			Dictionary<string, string?> settings,
			HttpMessageHandler? handler = null)
		{
			var configuration = new ConfigurationBuilder()
				.AddInMemoryCollection(settings)
				.Build();

			var httpClient = new HttpClient(handler ?? new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
			{
				Content = new StringContent("ok")
			}));

			return new GroqSpeechToTextService(httpClient, configuration, NullLogger<GroqSpeechToTextService>.Instance);
		}

		private static IFormFile CreateFormFile(byte[] content, string fileName)
		{
			var stream = new MemoryStream(content);
			var formFile = new FormFile(stream, 0, content.Length, "file", fileName)
			{
				Headers = new HeaderDictionary(),
				ContentType = "audio/mpeg"
			};

			return formFile;
		}

		private sealed class StubHttpMessageHandler : HttpMessageHandler
		{
			private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

			public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
			{
				_handler = handler;
			}

			protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
			{
				return Task.FromResult(_handler(request));
			}
		}
	}
}
