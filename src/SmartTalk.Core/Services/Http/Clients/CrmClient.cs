using System.Text;
using Newtonsoft.Json;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Settings.Crm;
using SmartTalk.Messages.Dto.AutoTest;
using SmartTalk.Messages.Dto.Crm;

namespace SmartTalk.Core.Services.Http.Clients;

public interface ICrmClient : IScopedDependency
{
    Task<string> GetCrmTokenAsync(CancellationToken cancellationToken);
    
    Task<List<CrmContactDto>> GetCustomerContactsAsync(string customerId, CancellationToken cancellationToken);

    Task<List<AutoTestCallLogDto>> GetCallRecordsAsync(DateTime startTimeUtc, DateTime endTimeUtc, CancellationToken cancellationToken);

    Task<List<GetCustomersPhoneNumberDataDto>> GetCustomersByPhoneNumberAsync(GetCustmoersByPhoneNumberRequestDto numberRequest, string token = null, CancellationToken cancellationToken = default);

    Task<List<CrmContactDto>> GetCustomerContactsAsync(string customerId, string token = null, CancellationToken cancellationToken = default);

    Task<List<GetDeliveryInfoByPhoneNumberResponseDto>> GetDeliveryInfoByPhoneNumberAsync(string phoneNumber, string apiKey = null, CancellationToken cancellationToken = default);
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

    public async Task<List<CrmContactDto>> GetCustomerContactsAsync(string customerId,
        CancellationToken cancellationToken)
    {
        var token = await GetCrmTokenAsync(cancellationToken).ConfigureAwait(false);

        var headers = new Dictionary<string, string>
        {
            { "Accept", "application/json" },
            { "Authorization", $"Bearer {token}" }
        };

        return await _httpClient
            .GetAsync<List<CrmContactDto>>($"{_crmSetting.BaseUrl}/api/customer/{customerId}/contacts",
                headers: headers, cancellationToken: cancellationToken).ConfigureAwait(false);
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

    public async Task<List<GetDeliveryInfoByPhoneNumberResponseDto>> GetDeliveryInfoByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            throw new ArgumentException("phoneNumber cannot be null or empty.", nameof(phoneNumber));
        
        var url = $"{_crmSetting.SyncBaseUrl}/api/external/get-delivery-info-by-phone-number?phone_number={phoneNumber}";

        var headers = new Dictionary<string, string>
        {
            { "X-API-KEY", _crmSetting.ApiKey },
            { "Accept", "application/json" }
        };

        return await _httpClient.GetAsync<List<GetDeliveryInfoByPhoneNumberResponseDto>>(url, cancellationToken: cancellationToken, headers: headers).ConfigureAwait(false);
    }
}