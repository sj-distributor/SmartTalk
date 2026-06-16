using System.Text;
using Newtonsoft.Json;
using Serilog;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Settings.Crm;
using SmartTalk.Messages.Dto.AutoTest;
using SmartTalk.Messages.Dto.Crm;
using SmartTalk.Messages.Dto.Sales;

namespace SmartTalk.Core.Services.Http.Clients;

public interface ICrmClient : IScopedDependency
{
    Task<string> GetCrmTokenAsync(CancellationToken cancellationToken);

    Task<List<GetCustomersPhoneNumberDataDto>> GetCustomersByPhoneNumberAsync(GetCustmoersByPhoneNumberRequestDto numberRequest, string token = null, CancellationToken cancellationToken = default);

    Task<List<GetCustomerIdByShopNameResponseDto>> GetCustomerIdsByShopNameAsync(string shopName, CancellationToken cancellationToken = default);

    Task<List<CrmContactDto>> GetCustomerContactsAsync(string customerId, string token = null, CancellationToken cancellationToken = default);

    Task<List<AutoTestCallLogDto>> GetCallRecordsAsync(DateTime startTimeUtc, DateTime endTimeUtc, CancellationToken cancellationToken);

    Task<List<GetDeliveryInfoByPhoneNumberResponseDto>> GetDeliveryInfoByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken = default);

    Task<(List<CrmSalesAutoSyncCustomerDto> Customers, int? TotalCount)> GetSalesAutoSyncCustomersAsync(int startPage = 1, bool isGetTotalCount = true, CancellationToken cancellationToken = default);

    Task<CrmSalesAutoSyncCustomerDto> GetSalesAutoSyncCustomerBySapIdAsync(string sapId, CancellationToken cancellationToken = default);
}

public class CrmClient : ICrmClient
{
    private const string CustomerIdsByShopNamePath = "/api/external/get-customers-by-shop-name?shop_name={0}";
    private readonly CrmSetting _crmSetting;
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
        var response = await _httpClient.PostAsync<CrmTokenResponse>(url, content, cancellationToken, headers: headers).ConfigureAwait(false);

        return response?.AccessToken;
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

        var result = await _httpClient.GetAsync<List<GetCustomersPhoneNumberDataDto>>(url, headers: headers, cancellationToken: cancellationToken).ConfigureAwait(false) ?? [];

        Log.Information("Found {Count} customers for phone {PhoneNumber}", result.Count, numberRequest.PhoneNumber);
        return result;
    }

    public async Task<List<GetCustomerIdByShopNameResponseDto>> GetCustomerIdsByShopNameAsync(string shopName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(shopName))
            return [];

        var headers = new Dictionary<string, string>
        {
            { "Accept", "application/json" },
            { "X-API-KEY", _crmSetting.ApiKey }
        };

        var url = $"{_crmSetting.SyncBaseUrl}{string.Format(CustomerIdsByShopNamePath, Uri.EscapeDataString(shopName))}";

        var result = await _httpClient.GetAsync<List<GetCustomerIdByShopNameResponseDto>>(url, headers: headers, cancellationToken: cancellationToken).ConfigureAwait(false) ?? [];

        Log.Information("Found {Count} customer ids for shop {ShopName}", result.Count, shopName);
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

        return response?.Data ?? [];
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

        var contacts = await _httpClient.GetAsync<List<CrmContactDto>>($"{_crmSetting.BaseUrl}/api/customer/{customerId}/contacts", headers: headers, cancellationToken: cancellationToken).ConfigureAwait(false) ?? [];
        Log.Information("Found {Count} contacts for customer {CustomerId}", contacts.Count, customerId);

        return contacts;
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

        return await _httpClient.GetAsync<List<GetDeliveryInfoByPhoneNumberResponseDto>>(url, cancellationToken: cancellationToken, headers: headers).ConfigureAwait(false) ?? [];
    }

    public async Task<(List<CrmSalesAutoSyncCustomerDto> Customers, int? TotalCount)> GetSalesAutoSyncCustomersAsync(int startPage = 1, bool isGetTotalCount = true, CancellationToken cancellationToken = default)
    {
        Log.Information("GetSalesAutoSyncCustomersAsync isGetTotalCount {@isGetTotalCount}", isGetTotalCount);
        
        var url = $"{_crmSetting.SyncBaseUrl}/api/external/get-customers-sales-follow-info";
        var headers = new Dictionary<string, string>
        {
            { "X-API-KEY", _crmSetting.ApiKey },
            { "Accept", "application/json" }
        };

        var page = startPage > 0 ? startPage : 1;
        var result = new List<CrmSalesAutoSyncCustomerDto>();

        while (true)
        {
            var pagedUrl = $"{url}?page={page}";
            var response = await _httpClient.GetAsync<CrmSalesAutoSyncPagedResponseDto>(pagedUrl, cancellationToken: cancellationToken, headers: headers).ConfigureAwait(false);

            if (response?.Data == null || response.Data.Count == 0)
                break;

            result.AddRange(response.Data);
            
            if (!isGetTotalCount)
            {
                return (result, response.Total);
            }

            if (response.CurrentPage >= response.LastPage)
                break;

            page++;
        }

        return (result, result.Count > 0 ? null : 0);
    }

    public async Task<CrmSalesAutoSyncCustomerDto> GetSalesAutoSyncCustomerBySapIdAsync(string sapId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sapId))
            throw new ArgumentException("sapId cannot be null or empty.", nameof(sapId));

        var url = $"{_crmSetting.SyncBaseUrl}/api/external/get-customer-sales-follow-info-by-sap-id?sap_id={Uri.EscapeDataString(sapId.Trim())}";
        var headers = new Dictionary<string, string>
        {
            { "X-API-KEY", _crmSetting.ApiKey },
            { "Accept", "application/json" }
        };

        return await _httpClient.GetAsync<CrmSalesAutoSyncCustomerDto>(url, cancellationToken: cancellationToken, headers: headers).ConfigureAwait(false);
    }
}
