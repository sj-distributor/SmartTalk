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

    private async Task<byte[]> ProcessAudioConversationAsync(List<byte[]> customerMp3List, string prompt, CancellationToken cancellationToken)
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
            AudioOptions = new ChatAudioOptions(ChatOutputAudioVoice.Alloy, ChatOutputAudioFormat.Mp3)
        };

        var allAudioSegments = new List<byte[]>();

        foreach (var userMp3 in customerMp3List)
        {
            if (userMp3 == null || userMp3.Length == 0)
                continue;

            allAudioSegments.Add(userMp3);

            conversationHistory.Add(new UserChatMessage(ChatMessageContentPart.CreateInputAudioPart(BinaryData.FromBytes(userMp3), ChatInputAudioFormat.Mp3)));

            var completion = await client.CompleteChatAsync(conversationHistory, options, cancellationToken);
            var aiMp3 = completion.Value.OutputAudio.AudioBytes.ToArray();

            allAudioSegments.Add(aiMp3);

            conversationHistory.Add(new AssistantChatMessage(completion.Value.OutputAudio.Transcript));
        }

        return MergeMp3Segments(allAudioSegments);
    }

// ---------------- 8kHz WAV -> 16kHz MP3 ----------------
    private byte[] ConvertWav8kToMp3_16k(byte[] wavBytes)
    {
        using var wavStream = new MemoryStream(wavBytes);
        using var reader = new WaveFileReader(wavStream);

        var outFormat = new WaveFormat(16000, 16, 1);
        using var resampler = new MediaFoundationResampler(reader, outFormat)
        {
            ResamplerQuality = 60
        };

        using var mp3Stream = new MemoryStream();
        using (var mp3Writer = new LameMP3FileWriter(mp3Stream, resampler.WaveFormat, LAMEPreset.STANDARD))
        {
            var buffer = new byte[resampler.WaveFormat.AverageBytesPerSecond];
            int read;
            while ((read = resampler.Read(buffer, 0, buffer.Length)) > 0)
            {
                mp3Writer.Write(buffer, 0, read);
            }
        }

        return mp3Stream.ToArray();
    }
    
    private byte[] MergeMp3Segments(List<byte[]> mp3Segments)
    {
        if (mp3Segments == null || mp3Segments.Count == 0)
            throw new ArgumentException("没有音频输入");

        using var finalStream = new MemoryStream();

        foreach (var segment in mp3Segments)
        {
            if (segment == null || segment.Length == 0)
                continue;

            finalStream.Write(segment, 0, segment.Length);
        }

        return finalStream.ToArray();
    }
}