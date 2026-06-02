using SmartTalk.Core.Ioc;
using SmartTalk.Core.Settings.SjFood;
using SmartTalk.Messages.Dto.SjFood;

namespace SmartTalk.Core.Services.Http.Clients;

public interface ISjFoodClient : IScopedDependency
{
    Task<GetCustomerAiQuotationResponseDto> GetCustomerAiQuotationAsync(GetCustomerAiQuotationRequestDto request, CancellationToken cancellationToken);
}

public class SjFoodClient : ISjFoodClient
{
    private readonly SjFoodSetting _sjFoodSetting;
    private readonly Dictionary<string, string> _headers;
    private readonly ISmartTalkHttpClientFactory _httpClientFactory;

    public SjFoodClient(SjFoodSetting sjFoodSetting, ISmartTalkHttpClientFactory httpClientFactory)
    {
        _sjFoodSetting = sjFoodSetting;
        _httpClientFactory = httpClientFactory;
        _headers = new Dictionary<string, string>
        {
            { "X-API-KEY", _sjFoodSetting.ApiKey }
        };
    }

    public async Task<GetCustomerAiQuotationResponseDto> GetCustomerAiQuotationAsync(GetCustomerAiQuotationRequestDto request, CancellationToken cancellationToken)
    {
        return await _httpClientFactory.PostAsJsonAsync<GetCustomerAiQuotationResponseDto>(
            $"{_sjFoodSetting.BaseUrl}/api/CustomerInfo/GetCustomerAiQuotation",
            request,
            headers: _headers,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
