using System.Text;
using Newtonsoft.Json;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Settings.Crm;
using SmartTalk.Messages.Dto.Crm;

namespace SmartTalk.Core.Services.Http.Clients;

public interface ICrmClient : IScopedDependency
{
    Task<string> GetCrmTokenAsync(CancellationToken cancellationToken);
    
    Task<List<GetCustomersPhoneNumberDataDto>> GetCustomersByPhoneNumberAsync(GetCustmoersByPhoneNumberRequestDto numberRequest, string token = null, CancellationToken cancellationToken = default);

    Task<List<CrmContactDto>> GetCustomerContactsAsync(string customerId, string token = null, CancellationToken cancellationToken = default);
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

        var payload = new Dictionary<string, string>
        {
            { "grant_type", "client_credentials" },
            { "client_id", _crmSetting.ClientId },
            { "client_secret", _crmSetting.ClientSecret }
        };
        
        var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
        
        var headers = new Dictionary<string, string>
        {
            { "Accept", "application/json" }
        };

        var resp = await _httpClient.PostAsync<CrmTokenResponse>(url, content, headers: headers, cancellationToken: cancellationToken).ConfigureAwait(false);

        return resp.AccessToken;
    }
    
    public async Task<List<GetCustomersPhoneNumberDataDto>> GetCustomersByPhoneNumberAsync(GetCustmoersByPhoneNumberRequestDto numberRequest, string token = null, CancellationToken cancellationToken = default)
    {
        token ??= await GetCrmTokenAsync(cancellationToken).ConfigureAwait(false);
        
        var headers = new Dictionary<string, string>
        {
            { "Accept", "application/json" },
            { "Authorization", $"Bearer {token}"}
        };
        
        var url = $"{_crmSetting.BaseUrl}/api/customer/get-customers-by-phone-number?phone_number={numberRequest.PhoneNumber}";

        return await _httpClient
            .GetAsync<List<GetCustomersPhoneNumberDataDto>>(url, headers: headers, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }
    
    public async Task<List<CrmContactDto>> GetCustomerContactsAsync(string customerId, string token = null, CancellationToken cancellationToken = default)
    {
        token ??= await GetCrmTokenAsync(cancellationToken).ConfigureAwait(false);

        var headers = new Dictionary<string, string>
        {
            { "Accept", "application/json" },
            { "Authorization", $"Bearer {token}" }
        };

        return await _httpClient.GetAsync<List<CrmContactDto>>($"{_crmSetting.BaseUrl}/api/customer/{customerId}/contacts", headers: headers, cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}