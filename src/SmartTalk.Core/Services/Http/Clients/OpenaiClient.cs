using System.Text;
using Newtonsoft.Json.Linq;
using Serilog;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Settings.OpenAi;
using SmartTalk.Messages.Dto.OpenAi;

namespace SmartTalk.Core.Services.Http.Clients;

public interface IOpenaiClient : IScopedDependency
{
    Task<string> InitialRealtimeSessionsAsync(string prompt, CancellationToken cancellationToken);
    
    Task<string> RealtimeChatAsync(string sdp,  string ephemeralToken, CancellationToken cancellationToken);
}

public class OpenaiClient : IOpenaiClient
{
    private readonly OpenAiSettings _openAiSettings;
    private readonly ISmartTalkHttpClientFactory _smartTalkHttpClientFactory;

    public OpenaiClient(OpenAiSettings openAiSettings, ISmartTalkHttpClientFactory smartTalkHttpClientFactory)
    {
        _openAiSettings = openAiSettings;
        _smartTalkHttpClientFactory = smartTalkHttpClientFactory;
    }

    public async Task<string> InitialRealtimeSessionsAsync(string prompt, CancellationToken cancellationToken)
    {
        var headers = new Dictionary<string, string>
        {
            { "Authorization", $"Bearer {_openAiSettings.ApiKey}" }
        };

        var requestUrl = $"{_openAiSettings.BaseUrl}/v1/realtime/sessions";
        
        var requestBody = new
        {
            model = "gpt-4o-realtime-preview-2024-12-17",
            voice = "verse",
            instructions = prompt
        };
        
        var response = await _smartTalkHttpClientFactory.PostAsJsonAsync<OpenAiRealtimeSessionsResponseDto>(
            requestUrl, requestBody, headers: headers, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        Log.Information("Get initial realtime session response: {@Response}", response);
        
        return response?.ClientSecret?.Value;
    }

    public async Task<string> RealtimeChatAsync(string sdp, string ephemeralToken, CancellationToken cancellationToken)
    {
        var headers = new Dictionary<string, string>
        {
            { "Authorization", $"Bearer {ephemeralToken}" }
        };
        
        var requestUrl = $"{_openAiSettings.BaseUrl}/v1/realtime/?model=gpt-4o-realtime-preview-2024-12-17";
        
        var requestContent = new StringContent(sdp, Encoding.UTF8, "application/sdp");
        
        return await _smartTalkHttpClientFactory.PostAsync<string>(
            requestUrl, requestContent, headers: headers, cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}