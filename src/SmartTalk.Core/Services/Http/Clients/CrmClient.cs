using System.Text;
using Newtonsoft.Json;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Settings.Crm;
using SmartTalk.Messages.Dto.Crm;

namespace SmartTalk.Core.Services.Http.Clients;

public interface ICrmClient : IScopedDependency
{
    Task<List<GetCustomersPhoneNumberDataDto>> GetCustomersByPhoneNumberAsync(GetCustmoersByPhoneNumberRequestDto numberRequest, CancellationToken cancellationToken);
}

public class CrmClient : ICrmClient
{
    private readonly Dictionary<string, string> _headers;
    private readonly CrmV3Setting _crmV3Setting;
    private readonly ISmartTalkHttpClientFactory _httpClient;

    public CrmClient(ISmartTalkHttpClientFactory httpClient, CrmV3Setting crmV3Setting)
    {
        _httpClient = httpClient;
        _crmV3Setting = crmV3Setting;
    }
    
    public async Task<List<GetCustomersPhoneNumberDataDto>> GetCustomersByPhoneNumberAsync(GetCustmoersByPhoneNumberRequestDto numberRequest, CancellationToken cancellationToken)
    {
        var headers = new Dictionary<string, string>
        {
            { "Accept", "application/json" },
            { "X-API-KEY", _crmV3Setting.ApiKey }
        };
        
        var url = $"{_crmV3Setting.BaseUrl}/api/external/get-customers-by-phone-number?phone_number={numberRequest.PhoneNumber}";

        return await _httpClient
            .GetAsync<List<GetCustomersPhoneNumberDataDto>>(url, headers: headers, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }
}