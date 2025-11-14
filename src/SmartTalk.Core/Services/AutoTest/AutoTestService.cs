using System.Diagnostics;
using AutoMapper;
using NAudio.Wave;
using NAudio.Lame;
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

    private async Task<byte[]> ProcessAudioConversationAsync(List<byte[]> customerWavList, string prompt, CancellationToken cancellationToken)
    {
        if (customerWavList == null || customerWavList.Count == 0)
            throw new ArgumentException("没有音频输入");

        var conversationHistory = new List<ChatMessage>
        {
            new SystemChatMessage(prompt)
        };

        var client = new ChatClient("gpt-4o-audio-preview", _openAiSettings.ApiKey);

        var options = new ChatCompletionOptions
        {
            ResponseModalities = ChatResponseModalities.Text | ChatResponseModalities.Audio,
            AudioOptions = new ChatAudioOptions(ChatOutputAudioVoice.Alloy, ChatOutputAudioFormat.Wav)
        };

        var allWavBytes = new List<byte[]>();

        foreach (var wavBytes in customerWavList)
        {
            if (wavBytes == null || wavBytes.Length == 0) continue;

            conversationHistory.Add(new UserChatMessage(
                ChatMessageContentPart.CreateInputAudioPart(
                    BinaryData.FromBytes(wavBytes),
                    ChatInputAudioFormat.Wav)));

            var completion = await client.CompleteChatAsync(conversationHistory, options, cancellationToken);

            allWavBytes.Add(completion.Value.OutputAudio.AudioBytes.ToArray());

            conversationHistory.Add(new AssistantChatMessage(completion.Value.OutputAudio.Transcript));
        }

        var mergedWavBytes = MergeWavBytes(allWavBytes);

        return mergedWavBytes;
    }


    public static byte[] MergeWavBytes(List<byte[]> wavByteList)
    {
        if (wavByteList == null || wavByteList.Count == 0)
            throw new ArgumentException("没有 WAV 数据可合并");

        using var outputStream = new MemoryStream();

        byte[] header = null;
        var pcmDataList = new List<byte[]>();

        foreach (var wavBytes in wavByteList)
        {
            if (wavBytes == null || wavBytes.Length == 0) continue;

            var pcmData = wavBytes.Skip(44).ToArray();
            pcmDataList.Add(pcmData);

            if (header == null)
            {
                header = wavBytes.Take(44).ToArray();
            }
        }

        int totalPcmLength = pcmDataList.Sum(p => p.Length);

        byte[] outputHeader = new byte[44];
        Array.Copy(header, outputHeader, 44);

        int chunkSize = 36 + totalPcmLength;
        BitConverter.GetBytes(chunkSize).CopyTo(outputHeader, 4);

        BitConverter.GetBytes(totalPcmLength).CopyTo(outputHeader, 40);

        outputStream.Write(outputHeader, 0, 44);

        foreach (var pcm in pcmDataList)
        {
            outputStream.Write(pcm, 0, pcm.Length);
        }

        return outputStream.ToArray();
    }
}