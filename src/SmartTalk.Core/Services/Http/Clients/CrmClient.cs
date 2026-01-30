using System.Text;
using Newtonsoft.Json;
using Serilog;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Settings.Crm;
using SmartTalk.Messages.Dto.AutoTest;
using SmartTalk.Messages.Dto.Crm;

namespace SmartTalk.Core.Services.Http.Clients;

public interface ICrmClient : IScopedDependency
{
    Task<string> GetCrmTokenAsync(CancellationToken cancellationToken);
    
    Task<List<CrmContactDto>> GetCustomerContactsAsync(string customerId, string token = null, CancellationToken cancellationToken = default);

    Task<List<GetCustomersPhoneNumberDataDto>> GetCustomersByPhoneNumberAsync(GetCustmoersByPhoneNumberRequestDto numberRequest, CancellationToken cancellationToken);
    
    Task<List<AutoTestCallLogDto>> GetCallRecordsAsync(DateTime startTimeUtc, DateTime endTimeUtc, CancellationToken cancellationToken);
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

        Log.Information("Requesting CRM token from {Url} with client_id={ClientId}", url, _crmSetting.ClientId);
        
        var resp = await _httpClient.PostAsync<CrmTokenResponse>(url, content, headers: headers, cancellationToken: cancellationToken).ConfigureAwait(false);
        Log.Information("CRM token resp: {resp}", resp);

        return resp.AccessToken;
    }

    public async Task<List<CrmContactDto>> GetCustomerContactsAsync(string customerId, string token = null, CancellationToken cancellationToken = default)
    {
        token ??= await GetCrmTokenAsync(cancellationToken).ConfigureAwait(false);

        var headers = new Dictionary<string, string>
        {
            { "Accept", "application/json" },
            { "Authorization", $"Bearer {token}" }
        };
        
        Log.Information("Fetching contacts for customer {CustomerId}", customerId);
        
        var contacts = await _httpClient.GetAsync<List<CrmContactDto>>($"{_crmSetting.BaseUrl}/api/customer/{customerId}/contacts", headers: headers, cancellationToken: cancellationToken).ConfigureAwait(false);
        Log.Information("Found {Count} contacts for customer {CustomerId}", contacts.Count, customerId);
        
        return contacts;
    }

    public async Task<List<GetCustomersPhoneNumberDataDto>> GetCustomersByPhoneNumberAsync(GetCustmoersByPhoneNumberRequestDto numberRequest, CancellationToken cancellationToken)
    {
        var  token = await GetCrmTokenAsync(cancellationToken);
        
        var headers = new Dictionary<string, string>
        {
            { "Accept", "application/json" },
            { "Authorization", $"Bearer {token}"}
        };
        
        var url = $"{_crmSetting.BaseUrl}/api/customer/get-customers-by-phone-number?phone_number={numberRequest.PhoneNumber}";

        var result = await _httpClient.GetAsync<List<GetCustomersPhoneNumberDataDto>>(url, headers: headers, cancellationToken: cancellationToken).ConfigureAwait(false);

        Log.Information("Found {Count} customers for phone {PhoneNumber}", result.Count, numberRequest.PhoneNumber);
        return result;
    }
    
    public async Task<List<AutoTestCallLogDto>> GetCallRecordsAsync(DateTime startTimeUtc, DateTime endTimeUtc, CancellationToken cancellationToken)
    {
        var url = $"{_crmSetting.SyncBaseUrl}/api/external/ring-central/call-logs" + $"?start_time={startTimeUtc:O}&end_time={endTimeUtc:O}";
        
        var headers = new Dictionary<string, string>
        {
            { "Accept", "application/json" },
            { "X-API-KEY", _crmSetting.ApiKey }
        };
        
        var response = await _httpClient.GetAsync<GetCallRecordsDataDto>(url, headers: headers, cancellationToken: cancellationToken).ConfigureAwait(false);

        return response.Data;
    }
}