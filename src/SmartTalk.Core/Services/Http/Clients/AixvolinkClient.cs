using SmartTalk.Core.Ioc;
using SmartTalk.Core.Settings.PhoneOrder;
using SmartTalk.Messages.Dto.Aixvolink;

namespace SmartTalk.Core.Services.Http.Clients;

public interface IAixvolinkClient : IScopedDependency
{
    Task CallResultsCallbackAsync(AixvolinkCallResultsCallbackRequest request, CancellationToken cancellationToken);
}

public class AixvolinkClient : IAixvolinkClient
{
    private readonly AixvolinkApiSetting _setting;
    private readonly ISmartTalkHttpClientFactory _httpClientFactory;

    public AixvolinkClient(AixvolinkApiSetting setting, ISmartTalkHttpClientFactory httpClientFactory)
    {
        _setting = setting;
        _httpClientFactory = httpClientFactory;
    }

    public async Task CallResultsCallbackAsync(AixvolinkCallResultsCallbackRequest request, CancellationToken cancellationToken)
    {
        var headers = new Dictionary<string, string>
        {
            { "API-KEY", _setting.ApiKey }
        };

        await _httpClientFactory.PostAsJsonAsync<object>($"{_setting.BaseUrl}/api/external/smarttalk/call-results/callback", request, cancellationToken, headers: headers).ConfigureAwait(false);
    }
}
