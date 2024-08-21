using SmartTalk.Core.Ioc;
using SmartTalk.Messages.Dto.Smarties;
using SmartTalk.Core.Settings.Smarties;

namespace SmartTalk.Core.Services.Http.Clients;

public interface ISmartiesClient : IScopedDependency
{
    Task<AskGptResponse> PerformQueryAsync(AskGptRequestDto request, CancellationToken cancellationToken);
}

public class SmartiesClient : ISmartiesClient
{
    private readonly  SmartiesSetting _smartiesSettings;
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
    
    public async Task<AskGptResponse> PerformQueryAsync(AskGptRequestDto request, CancellationToken cancellationToken)
    {
        return await _httpClientFactory.PostAsJsonAsync<AskGptResponse>(
            $"{_smartiesSettings.BaseUrl}/api/Ask/general/query", request, cancellationToken, headers: _headers).ConfigureAwait(false);
    }
}