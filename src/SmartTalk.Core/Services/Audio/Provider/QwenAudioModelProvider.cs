using SmartTalk.Core.Services.Http;
using SmartTalk.Core.Settings.Qwen;
using SmartTalk.Messages.Commands.SpeechMatics;
using SmartTalk.Messages.Enums.Audio;

namespace SmartTalk.Core.Services.Audio.Provider;

public class QwenAudioModelProvider : IAudioModelProvider
{
    private const string BaseUrl = "http://47.77.223.168:8000/v1";
    private const string Model = "/root/autodl-tmp/Qwen3-Omni-30B-A3B-Instruct";

    private readonly QwenSettings _qwenSettings;
    private readonly ISmartTalkHttpClientFactory _httpClientFactory;

    public QwenAudioModelProvider(QwenSettings qwenSettings, ISmartTalkHttpClientFactory httpClientFactory)
    {
        _qwenSettings = qwenSettings;
        _httpClientFactory = httpClientFactory;
    }

    public AudioModelProviderType ModelProviderType { get; set; } = AudioModelProviderType.Qwen;

    public async Task<string> ExtractAudioDataFromModelProviderAsync(AnalyzeAudioCommand command, AudioService.AudioData audioData, CancellationToken cancellationToken)
    {
        if (audioData.Base64String == null)
        {
            throw new Exception("Audio base64 is empty for Qwen Audio Provider.");
        }

        var audioFormat = command.AudioFileFormat == AudioFileFormat.Wav ? "wav" : "mp3";

        var messages = new List<object>();

        if (!string.IsNullOrWhiteSpace(command.SystemPrompt))
        {
            messages.Add(new { role = "system", content = command.SystemPrompt });
        }

        var audioContent = new List<object>
        {
            new
            {
                type = "input_audio",
                input_audio = new
                {
                    data = audioData.Base64String,
                    format = audioFormat
                }
            }
        };

        if (!string.IsNullOrWhiteSpace(command.UserPrompt))
        {
            audioContent.Add(new { type = "text", text = command.UserPrompt });
        }

        messages.Add(new { role = "user", content = audioContent });

        var requestBody = new
        {
            model = Model,
            messages,
            modalities = new[] { "text" }
        };

        var headers = new Dictionary<string, string>
        {
            { "Authorization", $"Bearer {_qwenSettings.CrmApiKey}" }
        };

        var response = await _httpClientFactory.PostAsJsonAsync<QwenChatCompletionResponse>(
            $"{BaseUrl}/chat/completions",
            requestBody,
            cancellationToken,
            timeout: TimeSpan.FromMinutes(10),
            headers: headers).ConfigureAwait(false);

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