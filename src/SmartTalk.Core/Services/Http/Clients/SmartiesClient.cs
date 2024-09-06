using Serilog;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Settings.Smarties;
using SmartTalk.Messages.Dto.Smarties;

namespace SmartTalk.Core.Services.Http.Clients;

public interface ISmartiesClient : IScopedDependency
{
    Task<AskGptEmbeddingResponseDto> GetEmbeddingAsync(AskGptEmbeddingRequestDto request, CancellationToken cancellationToken);
}

public class SmartiesClient : ISmartiesClient
{
    private readonly SmartiesSetting _smartiesSetting;
    private readonly Dictionary<string, string> _header;
    private readonly ISmartTalkHttpClientFactory _httpClientFactory;

    public SmartiesClient(ISmartTalkHttpClientFactory httpClientFactory, SmartiesSetting smartiesSetting)
    {
        _smartiesSetting = smartiesSetting;
        _httpClientFactory = httpClientFactory;
        _header = new Dictionary<string, string>
        {
            { "X-API-KEY", smartiesSetting.ApiKey }
        };
    }

    public async Task<AskGptEmbeddingResponseDto> GetEmbeddingAsync(AskGptEmbeddingRequestDto request, CancellationToken cancellationToken)
    {
        Log.Information("Ask gpt embedding request: {@Request}", request);

        var response = await _httpClientFactory.PostAsJsonAsync<AskGptEmbeddingResponseDto>(_smartiesSetting.BaseUrl, request, cancellationToken, headers: _header).ConfigureAwait(false);
        
        Log.Information("Ask gpt embedding response: {@Response}", response);

        return response;
    }
}