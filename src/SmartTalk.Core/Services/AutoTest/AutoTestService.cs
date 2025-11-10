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

    private async Task<byte[]> ProcessAudioConversationAsync(List<byte[]> customerMp3List, string prompt,
        CancellationToken cancellationToken)
    {
        if (customerMp3List == null || customerMp3List.Count == 0)
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

        var allWavSegments = new List<byte[]>();

        foreach (var userMp3 in customerMp3List)
        {
            if (userMp3 == null || userMp3.Length == 0)
                continue;

            byte[] wavBytes;
            using (var mp3Stream = new MemoryStream(userMp3))
            using (var reader = new Mp3FileReader(mp3Stream))
            using (var msWav = new MemoryStream())
            {
                WaveFileWriter.WriteWavFileToStream(msWav, reader);
                wavBytes = msWav.ToArray();
            }

            allWavSegments.Add(wavBytes);

            conversationHistory.Add(new UserChatMessage(
                ChatMessageContentPart.CreateInputAudioPart(BinaryData.FromBytes(wavBytes), ChatInputAudioFormat.Wav)
            ));

            var completion = await client.CompleteChatAsync(conversationHistory, options, cancellationToken);

            var aiWav = completion.Value.OutputAudio.AudioBytes.ToArray();

            allWavSegments.Add(aiWav);

            conversationHistory.Add(new AssistantChatMessage(completion.Value.OutputAudio.Transcript));
        }

        return MergeWavSegments(allWavSegments);
    }

    private byte[] MergeWavSegments(List<byte[]> wavSegments)
    {
        if (wavSegments == null || wavSegments.Count == 0)
            throw new ArgumentException("没有音频输入");

        using var outputStream = new MemoryStream();
        WaveFileWriter? writer = null;

        foreach (var wavBytes in wavSegments)
        {
            if (wavBytes == null || wavBytes.Length == 0)
                continue;

            using var ms = new MemoryStream(wavBytes);
            using var reader = new WaveFileReader(ms);

            if (writer == null)
            {
                writer = new WaveFileWriter(outputStream, reader.WaveFormat);
            }

            var buffer = new byte[reader.WaveFormat.AverageBytesPerSecond];
            int bytesRead;
            while ((bytesRead = reader.Read(buffer, 0, buffer.Length)) > 0)
            {
                writer.Write(buffer, 0, bytesRead);
            }
        }

        writer?.Flush();
        return outputStream.ToArray();
    }
}