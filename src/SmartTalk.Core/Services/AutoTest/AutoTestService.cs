using AutoMapper;
using NAudio.Wave;
using OpenAI.Audio;
using OpenAI.Chat;
using Serilog;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Settings.OpenAi;
using SmartTalk.Messages.Commands.AutoTest;
using SmartTalk.Messages.Enums.AutoTest;

namespace SmartTalk.Core.Services.AutoTest;

public partial interface IAutoTestService : IScopedDependency
{
    Task<AutoTestRunningResponse> AutoTestRunningAsync(AutoTestRunningCommand command, CancellationToken cancellationToken);

    Task<AutoTestConversationAudioProcessReponse> AutoTestConversationAudioProcessAsync(AutoTestConversationAudioProcessCommand command, CancellationToken cancellationToken);
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
        
        var taskRecords = await _autoTestDataProvider.GetPendingTaskRecordsByTaskIdAsync(command.TaskId, cancellationToken).ConfigureAwait(false);
        
        taskRecords.ForEach(x => x.Status = AutoTestTaskRecordStatus.Ongoing);
        
        await _autoTestDataProvider.UpdateTaskRecordsAsync(taskRecords, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        var executionResult = await _autoTestActionHandlerSwitcher.GetHandler(scenario.ActionType).ActionHandleAsync(scenario, command.TaskId, cancellationToken).ConfigureAwait(false);
        
        return new AutoTestRunningResponse() { Data = executionResult };
    }

    public async Task<AutoTestConversationAudioProcessReponse> AutoTestConversationAudioProcessAsync(AutoTestConversationAudioProcessCommand command, CancellationToken cancellationToken)
    {
        var coreAudioRecords = await ProcessAudioConversationAsync(command.CustomerAudioList, command.Prompt, cancellationToken);

        return new AutoTestConversationAudioProcessReponse()
        {
            Data = coreAudioRecords
        };
    }

    private async Task<byte[]> ProcessAudioConversationAsync(List<byte[]> customerPcmList, string prompt, CancellationToken cancellationToken)
    {
        if (customerPcmList == null || customerPcmList.Count == 0)
            throw new ArgumentException("没有音频输入");

        var conversationHistory = new List<ChatMessage>
        {
            new SystemChatMessage(prompt)
        };

        var client = new ChatClient("gpt-audio", _openAiSettings.ApiKey);

        var options = new ChatCompletionOptions
        {
            ResponseModalities = ChatResponseModalities.Text | ChatResponseModalities.Audio,
            AudioOptions = new ChatAudioOptions(ChatOutputAudioVoice.Alloy, ChatOutputAudioFormat.Pcm16)
        };

        using var combinedStream = new MemoryStream();

        foreach (var userPcm in customerPcmList)
        {
            var userWav = PcmToWav(userPcm, 16000, 16, 1);

            conversationHistory.Add(new UserChatMessage(
                ChatMessageContentPart.CreateInputAudioPart(BinaryData.FromBytes(userWav), ChatInputAudioFormat.Wav)
            ));

            var completion = await client.CompleteChatAsync(conversationHistory, options, cancellationToken);
            var aiPcmBytes = completion.Value.OutputAudio.AudioBytes.ToArray();
            var aiReplyText = completion.Value.OutputAudio.Transcript;

            combinedStream.Write(userPcm, 0, userPcm.Length);
            combinedStream.Write(aiPcmBytes, 0, aiPcmBytes.Length);

            conversationHistory.Add(new AssistantChatMessage(aiReplyText));
        }

        return combinedStream.ToArray();
    }


    private static byte[] PcmToWav(byte[] pcmData, int sampleRate, int bitsPerSample, int channels)
    {
        using var ms = new MemoryStream();
        var waveFormat = new WaveFormat(sampleRate, bitsPerSample, channels);
        using (var writer = new WaveFileWriter(ms, waveFormat))
        {
            writer.Write(pcmData, 0, pcmData.Length);
            writer.Flush();
        }
        return ms.ToArray();
    }
}