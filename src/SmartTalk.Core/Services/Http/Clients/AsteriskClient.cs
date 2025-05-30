using SmartTalk.Core.Ioc;
using SmartTalk.Core.Settings.Linphone;
using SmartTalk.Messages.Dto.Linphone;
using SmartTalk.Messages.Requests.Twilio;

namespace SmartTalk.Core.Services.Http.Clients;

public interface IAsteriskClient : IScopedDependency
{
    Task<GetAsteriskCdrResponseDto> GetAsteriskCdrAsync(string number, string asteriskBaseUrl, CancellationToken cancellationToken);
    
    Task<GetLinphoneCdrResponseDto> GetLinphoneCdrAsync(string lastTime, CancellationToken cancellationToken);
}

public class AsteriskClient : IAsteriskClient
{
    private LinphoneSetting _linphoneSetting;
    private ISmartTalkHttpClientFactory _smartTalkHttpClientFactory;

    public AsteriskClient(LinphoneSetting linphoneSetting, ISmartTalkHttpClientFactory smartTalkHttpClientFactory)
    {
        _linphoneSetting = linphoneSetting;
        _smartTalkHttpClientFactory = smartTalkHttpClientFactory;
    }

    public async Task<GetAsteriskCdrResponseDto> GetAsteriskCdrAsync(string number, string asteriskBaseUrl, CancellationToken cancellationToken)
    {
        return await _smartTalkHttpClientFactory.GetAsync<GetAsteriskCdrResponseDto>($"{asteriskBaseUrl}/api/cdr?src_number={number}", cancellationToken).ConfigureAwait(false);
    }
    
    public async Task<GetLinphoneCdrResponseDto> GetLinphoneCdrAsync(string lastTime, CancellationToken cancellationToken)
    {
        return await _smartTalkHttpClientFactory.GetAsync<GetLinphoneCdrResponseDto>($"{_linphoneSetting.BaseUrl}/api/history?last_time={lastTime}", cancellationToken).ConfigureAwait(false);
    }
}