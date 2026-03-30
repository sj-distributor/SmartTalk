using OpenAI.Chat;
using SmartTalk.Core.Extensions;
using SmartTalk.Core.Services.Http;
using SmartTalk.Core.Settings.OpenAi;
using SmartTalk.Messages.Commands.SpeechMatics;
using SmartTalk.Messages.Enums.Audio;

namespace SmartTalk.Core.Services.Audio.Provider;

public class OpenAiAudioModelProvider : IAudioModelProvider
{
    private readonly OpenAiSettings _openAiSettings;
    private readonly ISmartTalkHttpClientFactory _httpClientFactory;

    public OpenAiAudioModelProvider(OpenAiSettings openAiSettings, ISmartTalkHttpClientFactory httpClientFactory)
    {
        _openAiSettings = openAiSettings;
        _httpClientFactory = httpClientFactory;
    }

    public AudioModelProviderType ModelProviderType { get; set; } = AudioModelProviderType.OpenAi;
    
    public async Task<string> ExtractAudioDataFromModelProviderAsync(AnalyzeAudioCommand command, BinaryData audioData, CancellationToken cancellationToken)
    {
        var messages = new List<ChatMessage>();
        if (!string.IsNullOrWhiteSpace(command.SystemPrompt))
            messages.Add(new SystemChatMessage(command.SystemPrompt));

        messages.Add(new UserChatMessage(ChatMessageContentPart.CreateInputAudioPart(audioData,
            command.AudioFileFormat == AudioFileFormat.Wav ? ChatInputAudioFormat.Wav : ChatInputAudioFormat.Mp3)));

        if (!string.IsNullOrWhiteSpace(command.UserPrompt))
            messages.Add(new UserChatMessage(command.UserPrompt));

        var options = new ChatCompletionOptions { ResponseModalities = ChatResponseModalities.Text };
        
        return await _openAiSettings.ExecuteWithApiKeyFailoverAsync(
            async apiKey =>
            {
                var client = new ChatClient("gpt-4o-audio-preview", apiKey);
                ChatCompletion completion = await client
                    .CompleteChatAsync(messages, options, cancellationToken)
                    .ConfigureAwait(false);

                return completion.Content.FirstOrDefault()?.Text;
            },
            isSuccess: text => !string.IsNullOrWhiteSpace(text),
            operationName: nameof(ExtractAudioDataFromModelProviderAsync),
            throwIfAllFailed: true,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
