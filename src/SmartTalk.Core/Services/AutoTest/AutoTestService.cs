using AutoMapper;
using NAudio.Wave;
using OpenAI.Audio;
using OpenAI.Chat;
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

        var responseRecords = coreAudioRecords.Select(r => new SmartTalk.Messages.Commands.AutoTest.AudioConversationRecord
        {
            UserAudio = r.UserAudio,
            AiAudio = r.AiAudio,
            AiText = r.AiText
        }).ToList();

        return new AutoTestConversationAudioProcessReponse()
        {
            Data = responseRecords
        };
    }

    public async Task<List<AudioConversationRecord>> ProcessAudioConversationAsync(List<byte[]> customerAudioList, string prompt, CancellationToken cancellationToken)
    {
        var conversationHistory = new List<ChatMessage>
        {
            new SystemChatMessage(prompt)
        };

        var client = new ChatClient("gpt-audio", _openAiSettings.ApiKey);

        var options = new ChatCompletionOptions
        {
            ResponseModalities = ChatResponseModalities.Text | ChatResponseModalities.Audio,
            AudioOptions = new ChatAudioOptions(ChatOutputAudioVoice.Alloy, ChatOutputAudioFormat.Wav)
        };

        ChatCompletion? lastCompletion = null;

        var transcriptionClient = new AudioClient("gpt-audio", _openAiSettings.ApiKey);
        
        var transcriptionOptions = new AudioTranscriptionOptions
        {
            Language = "zh",
            ResponseFormat = AudioTranscriptionFormat.Text
        };

        var conversationRecords = new List<AudioConversationRecord>();

        foreach (var customerAudio in customerAudioList)
        {
            conversationHistory.Add(
                new UserChatMessage(
                    ChatMessageContentPart.CreateInputAudioPart(
                        BinaryData.FromBytes(customerAudio),
                        ChatInputAudioFormat.Wav
                    )
                )
            );

            var completion = await client.CompleteChatAsync(conversationHistory, options, cancellationToken);
            var aiAudioBytes = completion.Value.OutputAudio.AudioBytes.ToArray();
            var aiReplyText = completion.Value.OutputAudio.Transcript;
            
            using var audioStream = new MemoryStream(aiAudioBytes);

            conversationHistory.Add(new AssistantChatMessage(aiReplyText));

            conversationRecords.Add(new AudioConversationRecord
            {
                UserAudio = customerAudio,
                AiAudio = aiAudioBytes,
                AiText = aiReplyText
            });
        }

        return conversationRecords;
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
    
    private static byte[] ExtractPcmFromWav(byte[] wavData)
    {
        using var ms = new MemoryStream(wavData);
        using var rdr = new WaveFileReader(ms);
        using var pcmStream = new MemoryStream();
        rdr.CopyTo(pcmStream);
        return pcmStream.ToArray();
    }

    private static byte[] ConcatPcmSegments(List<byte[]> segments)
    {
        int totalLength = segments.Sum(s => s.Length);
        byte[] result = new byte[totalLength];
        int offset = 0;
        foreach (var s in segments)
        {
            Buffer.BlockCopy(s, 0, result, offset, s.Length);
            offset += s.Length;
        }

        return result;
    }
    
    public class AudioConversationRecord
    {
        public byte[] UserAudio { get; set; }
        public byte[] AiAudio { get; set; }
        public string AiText { get; set; }
    }
}