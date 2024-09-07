using Serilog;
using SmartTalk.Core.Ioc;
using Smarties.Messages.Requests.Ask;
using SmartTalk.Core.Settings.Smarties;
using SmartTalk.Messages.Dto.Smarties;

namespace SmartTalk.Core.Services.Http.Clients;

public interface ISmartiesClient : IScopedDependency
{
    Task<AskGptResponse> PerformQueryAsync(AskGptRequest request, CancellationToken cancellationToken);
    
    Task<AskGptEmbeddingResponseDto> GetEmbeddingAsync(AskGptEmbeddingRequestDto request, CancellationToken cancellationToken);
}

public class SmartiesClient : ISmartiesClient
{
    private readonly SmartiesSetting _smartiesSettings;
    private readonly Dictionary<string, string> _headers;
    private readonly ISmartTalkHttpClientFactory _httpClientFactory;
    
    public SmartiesClient(SmartiesSetting smartiesSettings, ISmartTalkHttpClientFactory httpClientFactory)
    {
        _smartiesSettings = smartiesSettings;
        _httpClientFactory = httpClientFactory;
        
        _headers = new Dictionary<string, string>
        {
            { "X-API-KEY", _smartiesSettings.ApiKey }
        };
    }
    
    public async Task<AskGptResponse> PerformQueryAsync(AskGptRequest request, CancellationToken cancellationToken)
    {
        return await _httpClientFactory.PostAsJsonAsync<AskGptResponse>(
            $"{_smartiesSettings.BaseUrl}/api/Ask/general/query", request, cancellationToken, headers: _headers).ConfigureAwait(false);
    }
    
    public async Task<AskGptEmbeddingResponseDto> GetEmbeddingAsync(AskGptEmbeddingRequestDto request, CancellationToken cancellationToken)
    {
        Log.Information("Ask gpt embedding request: {@Request}", request);

        var response = await _httpClientFactory.PostAsJsonAsync<AskGptEmbeddingResponseDto>(_smartiesSetting.BaseUrl, request, cancellationToken, headers: _header).ConfigureAwait(false);
        
        Log.Information("Ask gpt embedding response: {@Response}", response);

        return response;
    }
}