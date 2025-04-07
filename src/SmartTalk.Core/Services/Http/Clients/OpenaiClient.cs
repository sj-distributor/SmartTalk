using System.Text;
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
        if (string.IsNullOrWhiteSpace(ephemeralToken)) 
            throw new ArgumentException("Ephemeral token cannot be null or empty.");
        
        var headers = new Dictionary<string, string>
        {
            { "Authorization", $"Bearer {ephemeralToken}" },
            { "Content-Type", "application/sdp" },
            { "User-Agent", "Chrome/122.0.0.0" }
        };
        
        var requestUrl = $"{_openAiSettings.BaseUrl}/v1/realtime/?model=gpt-4o-realtime-preview-2024-12-17";
        
        var requestContent = new StringContent(sdp, Encoding.UTF8, "application/sdp");
        
        var response = await _smartTalkHttpClientFactory.PostAsync(
            requestUrl, requestContent, headers: headers, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        Log.Information("Start realtime connection response: {@Response}", response);
        
        if ((int)response.StatusCode == 307 && response.Headers.Location != null)
        {
            Log.Information("Start realtime redirect");
            
            var redirectUri = response.Headers.Location;

            if (redirectUri.Scheme == "http")
            {
                var builder = new UriBuilder(redirectUri) { Scheme = "https", Port = 443 };
                
                redirectUri = builder.Uri;
            }
            
            var retryContent = new StringContent(sdp, Encoding.UTF8, "application/sdp");

            var retryResponse = await _smartTalkHttpClientFactory.PostAsync(
                redirectUri.ToString(), retryContent, headers: headers, cancellationToken: cancellationToken).ConfigureAwait(false);

            return await retryResponse.Content.ReadAsStringAsync(cancellationToken);
        }

        return string.Empty;
    }
}