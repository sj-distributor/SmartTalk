using OpenAI.Chat;
using Serilog;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.Http;
using SmartTalk.Core.Settings.OpenAi;
using SmartTalk.Messages.Commands.SpeechMatics;
using SmartTalk.Messages.Enums.Audio;

namespace SmartTalk.Core.Services.Audio;

public interface IAudioService : IScopedDependency
{
    Task<AnalyzeAudioResponse> AnalyzeAudioAsync(AnalyzeAudioCommand command, CancellationToken cancellationToken);
}

public class AudioService : IAudioService
{
    private readonly OpenAiSettings _openAiSettings;
    private readonly ISmartTalkHttpClientFactory _httpClientFactory;

    public AudioService(OpenAiSettings openAiSettings, ISmartTalkHttpClientFactory httpClientFactory)
    {
        _openAiSettings = openAiSettings;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<AnalyzeAudioResponse> AnalyzeAudioAsync(AnalyzeAudioCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var client = new ChatClient("gpt-4o-audio-preview", _openAiSettings.ApiKey);

        var audioContent = !string.IsNullOrWhiteSpace(command.AudioUrl)
            ? await _httpClientFactory.GetAsync<byte[]>(command.AudioUrl, cancellationToken).ConfigureAwait(false)
            : command.AudioContent;

        if (audioContent is null || audioContent.Length == 0)
            throw new InvalidOperationException("Audio content is empty.");

        var audioData = BinaryData.FromBytes(audioContent);

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

        Log.Information("Audio analysis result: {Analysis}", resultText);

        return new AnalyzeAudioResponse
        {
            Data = resultText
        };
    }
}