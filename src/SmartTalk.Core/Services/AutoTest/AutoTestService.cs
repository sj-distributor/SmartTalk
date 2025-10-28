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

    private async Task<byte[]> ProcessAudioConversationAsync(List<byte[]> customerAudioList, string prompt, CancellationToken cancellationToken)
    {
        if (customerAudioList == null || customerAudioList.Count == 0)
            throw new ArgumentException("没有音频输入");

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

        using var combinedStream = new MemoryStream();
        var targetFormat = new WaveFormat(16000, 16, 1);
        WaveFileWriter waveWriter = new WaveFileWriter(combinedStream, targetFormat);

        foreach (var customerAudio in customerAudioList)
        {
            if (customerAudio == null || customerAudio.Length == 0)
            {
                Log.Warning("跳过空音频文件");
                continue;
            }

            LogAudioParameters("用户输入音频", customerAudio);

            conversationHistory.Add(new UserChatMessage(
                ChatMessageContentPart.CreateInputAudioPart(
                    BinaryData.FromBytes(customerAudio),
                    ChatInputAudioFormat.Wav
                )
            ));

            var completion = await client.CompleteChatAsync(conversationHistory, options, cancellationToken);
            var aiAudioBytes = completion.Value.OutputAudio.AudioBytes.ToArray();
            var aiReplyText = completion.Value.OutputAudio.Transcript;

            LogAudioParameters("AI 回复音频", aiAudioBytes);

            AppendAudioToWave(customerAudio, waveWriter, targetFormat);
            AppendAudioToWave(aiAudioBytes, waveWriter, targetFormat);

            conversationHistory.Add(new AssistantChatMessage(aiReplyText));
        }
        
        waveWriter.Dispose();

        return combinedStream.ToArray();
    }

    private void AppendAudioToWave(byte[] audioBytes, WaveFileWriter waveWriter, WaveFormat targetFormat)
    {
        if (audioBytes == null || audioBytes.Length == 0)
            return;

        using var ms = new MemoryStream(audioBytes);
        using var reader = new WaveFileReader(ms);

        IWaveProvider provider;

        if (!reader.WaveFormat.Equals(targetFormat))
        {
            var resampler = new MediaFoundationResampler(reader, targetFormat)
            {
                ResamplerQuality = 60
            };
            provider = resampler;
        }
        else
        {
            provider = reader;
        }

        byte[] buffer = new byte[2_097_152];
        int bytesRead;
        while ((bytesRead = provider.Read(buffer, 0, buffer.Length)) > 0)
        {
            waveWriter.Write(buffer, 0, bytesRead);
        }
    }

    private void LogAudioParameters(string tag, byte[] audioBytes)
    {
        if (audioBytes == null || audioBytes.Length == 0) return;

        using var ms = new MemoryStream(audioBytes);
        using var reader = new WaveFileReader(ms);

        Log.Information(
            "{Tag} 参数：采样率 {SampleRate}Hz, 位深 {Bits}bit, 声道 {Channels}",
            tag,
            reader.WaveFormat.SampleRate,
            reader.WaveFormat.BitsPerSample,
            reader.WaveFormat.Channels
        );
    }

}