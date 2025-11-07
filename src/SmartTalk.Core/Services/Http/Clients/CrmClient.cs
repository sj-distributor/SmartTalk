using SmartTalk.Core.Ioc;
using SmartTalk.Core.Settings.Crm;
using SmartTalk.Messages.Dto.Crm;

namespace SmartTalk.Core.Services.Http.Clients;

public interface ICrmClient : IScopedDependency
{
    Task<string> GetCrmTokenAsync(CancellationToken cancellationToken);
    
    Task<List<CrmContactDto>> GetCustomerContactsAsync(string customerId, CancellationToken cancellationToken);
}

public class CrmClient : ICrmClient
{
    private readonly CrmSetting _crmSetting;
    private readonly Dictionary<string, string> _headers;
    private readonly ISmartTalkHttpClientFactory _httpClient;

    public CrmClient(ISmartTalkHttpClientFactory httpClient, CrmSetting crmSetting)
    {
        _httpClient = httpClient;
        _crmSetting = crmSetting;
    }

    public async Task<string> GetCrmTokenAsync(CancellationToken cancellationToken)
    {
        var url = $"{_crmSetting.BaseUrl}/oauth/token";

        var body = new
        {
            grant_type = "client_credentials",
            client_id = _crmSetting.ClientId,
            client_secret = _crmSetting.ClientSecret
        };

        var resp = await _httpClient.PostAsync<CrmTokenResponse>(url, body, cancellationToken: cancellationToken).ConfigureAwait(false);

        return resp.access_token;
    }

    public async Task<List<CrmContactDto>> GetCustomerContactsAsync(string customerId, CancellationToken cancellationToken)
    {
        var token = await GetCrmTokenAsync(cancellationToken).ConfigureAwait(false);

        var headers = new Dictionary<string, string>
        {
            { "Authorization", $"Bearer {token}" }
        };
        
        return await _httpClient.GetAsync<List<CrmContactDto>>($"{_crmSetting.BaseUrl}/api/customer/{customerId}/contacts", headers: headers, cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}