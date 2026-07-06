using SmartTalk.Core.Ioc;
using SmartTalk.Core.Settings.Daovika;
using SmartTalk.Messages.Dto.Daovika;

namespace SmartTalk.Core.Services.Http.Clients;

public interface IDaovikaClient : IScopedDependency
{
    Task<string> GetSalesGroupByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken);
}

public class DaovikaClient : IDaovikaClient
{
    private readonly DaovikaSetting _daovikaSetting;
    private readonly ISmartTalkHttpClientFactory _httpClientFactory;

    public DaovikaClient(DaovikaSetting daovikaSetting, ISmartTalkHttpClientFactory httpClientFactory)
    {
        _daovikaSetting = daovikaSetting;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<string> GetSalesGroupByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            return string.Empty;

        if (string.IsNullOrWhiteSpace(_daovikaSetting.SalesGroupTableId))
            throw new InvalidOperationException("Daovika:SalesGroupTableId is not configured.");

        var queryPhoneNumber = phoneNumber.Trim();
        var url = $"{_daovikaSetting.BaseUrl}/api/external/table/query"
                  + $"?tableId={Uri.EscapeDataString(_daovikaSetting.SalesGroupTableId)}"
                  + $"&apiKey={Uri.EscapeDataString(_daovikaSetting.ApiKey)}"
                  + "&field=phoneNumber&op=eq"
                  + $"&value={Uri.EscapeDataString(queryPhoneNumber)}"
                  + "&limit=1000&offset=0";

        var headers = new Dictionary<string, string>
        {
            { "accept", "application/json" },
            { "x-api-key", _daovikaSetting.ApiKey }
        };

        var response = await _httpClientFactory.GetAsync<GetSalesGroupByPhoneNumberResponseDto>(url, headers: headers, cancellationToken: cancellationToken).ConfigureAwait(false);

        return response?.Rows?.FirstOrDefault()?.SalesGroup?.Trim() ?? string.Empty;
    }
}
