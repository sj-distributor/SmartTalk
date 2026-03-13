using OpenAI.Chat;
using Serilog;
using SmartTalk.Core.Ioc;
using System.Net.Http.Headers;
using SmartTalk.Core.Settings.OpenAi;
using SmartTalk.Messages.Dto.OpenAi;

namespace SmartTalk.Core.Services.Http.Clients;

public interface IOpenaiClient : IScopedDependency
{
    Task<string> InitialRealtimeSessionsAsync(OpenAiRealtimeSessionDto request, CancellationToken cancellationToken);
    
    Task<string> RealtimeChatAsync(string sdp,  string ephemeralToken, CancellationToken cancellationToken);

    Task<byte[]> GenerateAudioChatCompletionAsync(BinaryData audioData, string prompt, string voice, CancellationToken cancellationToken);
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

    public async Task<string> InitialRealtimeSessionsAsync(OpenAiRealtimeSessionDto request, CancellationToken cancellationToken)
    {
        var headers = new Dictionary<string, string>
        {
            { "Authorization", $"Bearer {_openAiSettings.ApiKey}" }
        };

        var requestUrl = $"{_openAiSettings.BaseUrl}/v1/realtime/sessions";
        
        var response = await _smartTalkHttpClientFactory.PostAsJsonAsync<OpenAiRealtimeSessionsResponseDto>(
            requestUrl, request, headers: headers, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        Log.Information("Initial realtime session response: {@Response}", response);
        
        return response?.ClientSecret?.Value;
    }

    public async Task<string> RealtimeChatAsync(string sdp, string ephemeralToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(ephemeralToken)) 
            throw new ArgumentException("Ephemeral token cannot be null or empty.");
        
        var headers = new Dictionary<string, string>
        {
            { "Authorization", $"Bearer {ephemeralToken}" }
        };
        
        var requestUrl = $"{_openAiSettings.BaseUrl}/v1/realtime?model=gpt-4o-realtime-preview-2024-12-17";
        
        var requestContent = new StringContent(sdp);
        requestContent.Headers.Clear();
        requestContent.Headers.ContentType = new MediaTypeHeaderValue("application/sdp");
        
        return await _smartTalkHttpClientFactory.PostAsync<string>(
            requestUrl, requestContent, headers: headers, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task<byte[]> GenerateAudioChatCompletionAsync(BinaryData audioData, string prompt, string voice, CancellationToken cancellationToken)
    {
        ChatClient client = new("gpt-4o-audio-preview", _openAiSettings.ApiKey);
        List<ChatMessage> messages =
        [
            new UserChatMessage(ChatMessageContentPart.CreateInputAudioPart(audioData, ChatInputAudioFormat.Wav)),
            new UserChatMessage(prompt)
        ];

        ChatCompletionOptions options = new()
        {
            ResponseModalities = ChatResponseModalities.Text | ChatResponseModalities.Audio,
            AudioOptions = new ChatAudioOptions(new ChatOutputAudioVoice(voice), ChatOutputAudioFormat.Wav)
        };

        ChatCompletion completion = await client.CompleteChatAsync(messages, options, cancellationToken);

        Log.Information("Analyze record to repeat order: {@completion}", completion);

        return completion.OutputAudio.AudioBytes.ToArray();
    }
}