using Microsoft.AspNetCore.Http;
using S2S.Shared.CommonResult;
using System.Threading;
using System.Threading.Tasks;

namespace S2S.ServicesAbstraction
{
	public interface ISpeechToTextService
	{
		Task<Result<string>> TranscribeAsync(IFormFile audio, string? language = null, CancellationToken cancellationToken = default);
	}
}
