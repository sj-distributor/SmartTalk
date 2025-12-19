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
    private const string Model = "Qwen3-Omni-30B-A3B-Instruct";

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
            model = Model,
            messages,
            modalities = new[] { "text" }
        };
        
        var headers = new Dictionary<string, string>
        {
            { "Authorization", $"Bearer {_qwenSettings.CrmApiKey}" }
        };
        
        var response = await _httpClientFactory.PostAsJsonAsync<QwenChatCompletionResponse>(
            $"{_qwenSettings.CrmApiKey}/chat/completions",
            requestBody,
            cancellationToken,
            timeout: TimeSpan.FromMinutes(10),
            headers: headers, 
            isNeedToReadErrorContent: true).ConfigureAwait(false);
        
        Log.Information("Received QwenChatCompletionResponse {@Response}", response);
        
        return response?.Choices?.FirstOrDefault()?.Message?.Content ?? string.Empty;
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