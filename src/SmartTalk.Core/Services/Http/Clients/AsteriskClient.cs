using SmartTalk.Core.Ioc;
using SmartTalk.Core.Settings.Asterisk;
using SmartTalk.Messages.Requests.Twilio;

namespace SmartTalk.Core.Services.Http.Clients;

public interface IAsteriskClient : IScopedDependency
{
    Task<GetAsteriskCdrResponseDto> GetAsteriskCdrAsync(string number, CancellationToken cancellationToken);
}

public class AsteriskClient : IAsteriskClient
{
    private AsteriskSetting _asteriskSetting;
    private ISmartTalkHttpClientFactory _smartTalkHttpClientFactory;

    public AsteriskClient(AsteriskSetting asteriskSetting, ISmartTalkHttpClientFactory smartTalkHttpClientFactory)
    {
        _asteriskSetting = asteriskSetting;
        _smartTalkHttpClientFactory = smartTalkHttpClientFactory;
    }

    public async Task<GetAsteriskCdrResponseDto> GetAsteriskCdrAsync(string number, CancellationToken cancellationToken)
    {
        return await _smartTalkHttpClientFactory.GetAsync<GetAsteriskCdrResponseDto>($"{_asteriskSetting.BaseUrl}/api/cdr?src_number={number}", cancellationToken).ConfigureAwait(false);
    }
}