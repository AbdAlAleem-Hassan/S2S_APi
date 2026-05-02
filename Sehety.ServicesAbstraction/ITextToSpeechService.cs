using S2S.Shared.CommonResult;
using System.Threading;
using System.Threading.Tasks;

namespace S2S.ServicesAbstraction
{
	public interface ITextToSpeechService
	{
		Task<Result<string>> SynthesizeAsync(string text, string? languageCode = null, CancellationToken cancellationToken = default);
	}
}
