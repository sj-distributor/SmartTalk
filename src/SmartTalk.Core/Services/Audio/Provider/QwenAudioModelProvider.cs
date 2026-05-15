using System.ClientModel;
using OpenAI;
using OpenAI.Chat;
using Serilog;
using SmartTalk.Core.Services.Http;
using SmartTalk.Core.Settings.Qwen;
using SmartTalk.Messages.Commands.SpeechMatics;
using SmartTalk.Messages.Enums.Audio;

namespace SmartTalk.Core.Services.Audio.Provider;

public class QwenAudioModelProvider : IAudioModelProvider
{
    private readonly QwenSettings _qwenSettings;
    private readonly ISmartTalkHttpClientFactory _httpClientFactory;

    public QwenAudioModelProvider(QwenSettings qwenSettings, ISmartTalkHttpClientFactory httpClientFactory)
    {
        _qwenSettings = qwenSettings;
        _httpClientFactory = httpClientFactory;
    }
    public AudioModelProviderType ModelProviderType { get; set; } = AudioModelProviderType.Qwen;
    
    public async Task<string> ExtractAudioDataFromModelProviderAsync(AnalyzeAudioCommand command, BinaryData audioData, CancellationToken cancellationToken)
    {
        var messages = new List<object>();
        var userInput = new List<object>();
        
        if (!string.IsNullOrWhiteSpace(command.SystemPrompt))
        {
            messages.Add(new { role = "system", content = command.SystemPrompt });
        }
        
        userInput.Add(new
        {
            type = "audio_url",
            audio_url = new
            {
                url = command.AudioUrl
            }
        });
        
        if (!string.IsNullOrWhiteSpace(command.UserPrompt))
        {
            userInput.Add(new { type = "text", text = command.UserPrompt });
        }
        
        messages.Add(new { role = "user", content = userInput });
        
        var requestBody = new
        {
            stream = false,
            model = _qwenSettings.CrmModel,
            messages,
            modalities = new[] { "text" }
        };
        
        var headers = new Dictionary<string, string>
        {
            { "Authorization", $"Bearer {_qwenSettings.CrmApiKey}" }
        };
        var requestUrl = $"{_qwenSettings.CrmBaseUrl}/chat/completions";
        
        Log.Information("LLM http call url: {CallUrl} ,headers: {CallHeader}, request body: {@requestBody}", requestUrl, ToMaskedHeadersForLog(headers), requestBody);

        var response = await _httpClientFactory.PostAsJsonAsync<QwenChatCompletionResponse>(
            requestUrl,
            requestBody,
            cancellationToken,
            timeout: TimeSpan.FromMinutes(10),
            headers: headers).ConfigureAwait(false);
        
        return response?.Choices?.FirstOrDefault()?.Message?.Content ?? string.Empty;
    }
    
    private IReadOnlyDictionary<string, string> ToMaskedHeadersForLog(IReadOnlyDictionary<string, string> headers)
    {
        if (headers == null)
            return new Dictionary<string, string>();

        return headers.ToDictionary(p => p.Key, p => MaskHeaderValue(p.Value));
    }
    
    private string MaskHeaderValue(string value)
    {
        const int visiblePrefixLength = 8;
        const int visibleSuffixLength = 4;
        const int minLengthToMask = visiblePrefixLength + visibleSuffixLength;

        if (string.IsNullOrEmpty(value) || value.Length <= minLengthToMask)
        {
            return value;
        }

        var middleMask = new string('*', value.Length - minLengthToMask);

        return $"{value[..visiblePrefixLength]}{middleMask}{value[^visibleSuffixLength..]}";
    }

    private class QwenChatCompletionResponse
    {
        public List<QwenChoice> Choices { get; set; }
    }

    private class QwenChoice
    {
        public QwenMessage Message { get; set; }
    }

    private class QwenMessage
    {
        public string Content { get; set; }
    }
}