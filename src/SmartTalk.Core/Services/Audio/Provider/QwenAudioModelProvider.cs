using System.ClientModel;
using OpenAI;
using OpenAI.Chat;
using SmartTalk.Core.Settings.Qwen;
using SmartTalk.Messages.Commands.SpeechMatics;
using SmartTalk.Messages.Enums.Audio;

namespace SmartTalk.Core.Services.Audio.Provider;

public class QwenAudioModelProvider : IAudioModelProvider
{
    private readonly QwenSettings _qwenSettings;

    public QwenAudioModelProvider(QwenSettings qwenSettings)
    {
        _qwenSettings = qwenSettings;
    }

    public AudioModelProviderType ModelProviderType { get; set; } = AudioModelProviderType.Qwen;
    
    public async Task<string> ExtractAudioDataFromModelProviderAsync(AnalyzeAudioCommand command, BinaryData audioData, CancellationToken cancellationToken)
    {
        var client = new ChatClient("/root/autodl-tmp/Qwen3-Omni-30B-A3B-Instruct", new ApiKeyCredential(_qwenSettings.CrmApiKey), new OpenAIClientOptions
        {
            Endpoint = new Uri("http://47.77.223.168:8000/v1"),
            NetworkTimeout = TimeSpan.FromMinutes(10)
        });

        var messages = new List<ChatMessage>();
        if (!string.IsNullOrWhiteSpace(command.SystemPrompt))
            messages.Add(new SystemChatMessage(command.SystemPrompt));

        messages.Add(new UserChatMessage(ChatMessageContentPart.CreateInputAudioPart(audioData,
            command.AudioFileFormat == AudioFileFormat.Wav ? ChatInputAudioFormat.Wav : ChatInputAudioFormat.Mp3)));

        if (!string.IsNullOrWhiteSpace(command.UserPrompt))
            messages.Add(new UserChatMessage(command.UserPrompt));

        var options = new ChatCompletionOptions { ResponseModalities = ChatResponseModalities.Text };

        ChatCompletion completion = await client
            .CompleteChatAsync(messages, options, cancellationToken)
            .ConfigureAwait(false);

        var resultText = completion.Content.FirstOrDefault()?.Text ?? string.Empty;

        return resultText;
    }
}