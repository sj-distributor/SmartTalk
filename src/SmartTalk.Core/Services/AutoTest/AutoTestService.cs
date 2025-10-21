using AutoMapper;
using NAudio.Wave;
using OpenAI.Chat;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.STT;
using SmartTalk.Core.Settings.OpenAi;
using SmartTalk.Messages.Commands.AutoTest;
namespace SmartTalk.Core.Services.AutoTest;

public partial interface IAutoTestService : IScopedDependency
{
    Task<AutoTestRunningResponse> AutoTestRunningAsync(AutoTestRunningCommand command, CancellationToken cancellationToken);
}

public partial class AutoTestService : IAutoTestService
{
    private readonly IMapper _mapper;
    private readonly IAutoTestDataProvider _autoTestDataProvider;
    private readonly IAutoTestActionHandlerSwitcher _autoTestActionHandlerSwitcher;
    private readonly IAutoTestDataImportHandlerSwitcher _autoTestDataImportHandlerSwitcher;
    private readonly OpenAiSettings _openAiSettings;
    public AutoTestService(IMapper mapper, IAutoTestDataProvider autoTestDataProvider, IAutoTestActionHandlerSwitcher autoTestActionHandlerSwitcher, IAutoTestDataImportHandlerSwitcher autoTestDataImportHandlerSwitcher,
        OpenAiSettings openAiSettings)
    {
        _mapper = mapper;
        _autoTestDataProvider = autoTestDataProvider;
        _autoTestActionHandlerSwitcher = autoTestActionHandlerSwitcher;
        _autoTestDataImportHandlerSwitcher = autoTestDataImportHandlerSwitcher;
        _openAiSettings = openAiSettings;
    }
    
    public async Task<AutoTestRunningResponse> AutoTestRunningAsync(AutoTestRunningCommand command, CancellationToken cancellationToken)
    {
        var scenario = await _autoTestDataProvider.GetAutoTestScenarioByIdAsync(command.ScenarioId, cancellationToken).ConfigureAwait(false);
        
        var executionResult = await _autoTestActionHandlerSwitcher.GetHandler(scenario.ActionType).ActionHandleAsync(scenario, cancellationToken).ConfigureAwait(false);
        
        return new AutoTestRunningResponse() { Data = executionResult };
    }
    
    public async Task<byte[]> ProcessAudioConversationAsync(List<byte[]> customerAudioList, string prompt, CancellationToken cancellationToken)
    {
        var conversationHistory = new List<ChatMessage>();
        conversationHistory.Add(new SystemChatMessage($"{prompt}"));
    
        var allAudioSegments = new List<byte[]>();
        var client = new ChatClient("gpt-4o-audio-preview", _openAiSettings.ApiKey);

        foreach (var customerAudio in customerAudioList)
        {
            conversationHistory.Add(new UserChatMessage(ChatMessageContentPart.CreateInputAudioPart(BinaryData.FromBytes(customerAudio), ChatInputAudioFormat.Wav)));
            var options = new ChatCompletionOptions { ResponseModalities = ChatResponseModalities.Text | ChatResponseModalities.Audio };
            var completion = await client.CompleteChatAsync(conversationHistory, options, cancellationToken);

            var aiReplyText = completion.Value.Content.FirstOrDefault()?.Text;
            
            conversationHistory.Add(new AssistantChatMessage(aiReplyText));
            
            var aiReplyAudio = completion.Value.OutputAudio.AudioBytes.ToArray();

            allAudioSegments.Add(customerAudio);
            
            allAudioSegments.Add(aiReplyAudio);
        }

        return await MergeAudiosAsync(allAudioSegments, cancellationToken);
    }
    
    private static async Task<byte[]> MergeAudiosAsync(List<byte[]> audioSegments, CancellationToken cancellationToken)
    {
        if (audioSegments == null || audioSegments.Count == 0)
            throw new ArgumentException("audioSegments is empty");

        var firstStream = new MemoryStream(audioSegments[0]);
        var firstReader = new WaveFileReader(firstStream);
        var waveFormat = firstReader.WaveFormat;

        using var outputStream = new MemoryStream();
        await using (var writer = new WaveFileWriter(outputStream, waveFormat))
        {
            foreach (var segment in audioSegments)
            {
                var ms = new MemoryStream(segment);
                var reader = new WaveFileReader(ms);

                if (!reader.WaveFormat.Equals(waveFormat))
                    throw new InvalidOperationException("All audio segments must have the same format!");

                byte[] buffer = new byte[1024];
                int bytesRead;
                while ((bytesRead = reader.Read(buffer, 0, buffer.Length)) > 0)
                {
                    writer.Write(buffer, 0, bytesRead);
                }
            }
        }

        return outputStream.ToArray();
    }
}