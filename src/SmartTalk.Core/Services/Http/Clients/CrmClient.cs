using SmartTalk.Core.Ioc;
using SmartTalk.Core.Settings.Crm;
using SmartTalk.Messages.Dto.Crm;

namespace SmartTalk.Core.Services.Http.Clients;

public interface ICrmClient : IScopedDependency
{
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
        
        _headers = new Dictionary<string, string>
        {
            { "Authorization", $"Bearer {_crmSetting.AccessToken}" }
        };
    }

    public async Task<List<CrmContactDto>> GetCustomerContactsAsync(string customerId, CancellationToken cancellationToken)
    {
        return await _httpClient.GetAsync<List<CrmContactDto>>($"{_crmSetting.BaseUrl}/api/customer/{customerId}/contacts", headers: _headers, cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}