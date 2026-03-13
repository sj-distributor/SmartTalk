namespace SmartTalk.Core.Services.AiSpeechAssistant;

using SmartTalk.Core.Ioc;
using TikaOnDotNet.TextExtraction;

public interface IFileTextExtractor : IScopedDependency
{
    Task<string> ExtractAsync(string fileUrl, CancellationToken cancellationToken);
}

public class FileTextExtractor : IFileTextExtractor
{
    private readonly IHttpClientFactory _httpClientFactory;

    public FileTextExtractor(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<string> ExtractAsync(string fileUrl, CancellationToken cancellationToken)
    {
        using var client = _httpClientFactory.CreateClient();
        var bytes = await client.GetByteArrayAsync(fileUrl, cancellationToken);

        var extractor = new TextExtractor();
        var result = extractor.Extract(bytes);

        return result?.Text ?? string.Empty;
    }
}
