using SmartTalk.Core.Ioc;
using SmartTalk.Messages.Requests.Twilio;

namespace SmartTalk.Core.Services.Http.Clients;

public interface IAsteriskClient : IScopedDependency
{
    Task<GetAsteriskCdrResponseDto> GetAsteriskCdrAsync(string number, string asteriskBaseUrl, CancellationToken cancellationToken);
}

public class AsteriskClient : IAsteriskClient
{
    private ISmartTalkHttpClientFactory _smartTalkHttpClientFactory;

    public AsteriskClient(ISmartTalkHttpClientFactory smartTalkHttpClientFactory)
    {
        _smartTalkHttpClientFactory = smartTalkHttpClientFactory;
    }

    public async Task<GetAsteriskCdrResponseDto> GetAsteriskCdrAsync(string number, string asteriskBaseUrl, CancellationToken cancellationToken)
    {
        return await _smartTalkHttpClientFactory.GetAsync<GetAsteriskCdrResponseDto>($"{asteriskBaseUrl}/api/cdr?src_number={number}", cancellationToken).ConfigureAwait(false);
    }
}