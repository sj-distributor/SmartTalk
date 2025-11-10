using SmartTalk.Core.Ioc;
using SmartTalk.Core.Settings.SapGateway;
using SmartTalk.Messages.Requests.AutoTest;

namespace SmartTalk.Core.Services.Http.Clients;

public interface ISapGatewayClients : IScopedDependency
{
    Task<QueryRecordingDataResponse> QueryRecordingDataAsync(QueryRecordingDataRequest request, CancellationToken cancellationToken);
}

public class SapGatewayClient : ISapGatewayClients
{
    private readonly Dictionary<string, string> _headers;
    private readonly SapGatewaySetting _sapGatewaySetting;
    private readonly ISmartTalkHttpClientFactory _httpClientFactory;

    public SapGatewayClient(SapGatewaySetting sapGatewaySetting, ISmartTalkHttpClientFactory httpClientFactory)
    {
        _sapGatewaySetting = sapGatewaySetting;
        _httpClientFactory = httpClientFactory;
        
        _headers = new Dictionary<string, string>
        {
            { "X-API-KEY", _sapGatewaySetting.ApiKey }
        };
    }
    
    public async Task<QueryRecordingDataResponse> QueryRecordingDataAsync(QueryRecordingDataRequest request, CancellationToken cancellationToken)
    {
        return await _httpClientFactory.PostAsJsonAsync<QueryRecordingDataResponse>($"{_sapGatewaySetting.BaseUrl}/api/app/bt/query-recording-data", request, headers: _headers, cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}