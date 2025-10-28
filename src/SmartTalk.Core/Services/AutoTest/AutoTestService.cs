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
        
        string outputFilePath = "combined_conversation.wav";
        WaveFileWriter? waveWriter = null;

        foreach (var customerAudio in customerAudioList)
        {
            if (customerAudio == null || customerAudio.Length == 0)
            {
                Log.Warning("跳过空音频文件");
                continue;
            }
            
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
            
            try
            {
                using var aiStream = new MemoryStream(aiAudioBytes);
                using var reader = new WaveFileReader(aiStream);

                Log.Information(
                    "AI 回复音频参数：采样率 {SampleRate}Hz, 位深 {Bits}bit, 声道 {Channels}",
                    reader.WaveFormat.SampleRate,
                    reader.WaveFormat.BitsPerSample,
                    reader.WaveFormat.Channels
                );

                if (waveWriter == null)
                {
                    waveWriter = new WaveFileWriter(outputFilePath, reader.WaveFormat);
                }

                AppendAudioToWave(customerAudio, waveWriter);

                reader.Position = 0;
                reader.CopyTo(waveWriter);
            }
            catch (Exception ex)
            {
                Log.Error("处理音频失败: {Message}", ex.Message);
                continue;
            }

            conversationHistory.Add(new AssistantChatMessage(aiReplyText));
        }

        waveWriter?.Dispose();

        Log.Information("拼接完成，输出文件：{File}", outputFilePath);

        return await File.ReadAllBytesAsync(outputFilePath, cancellationToken);
    }
    
    private void AppendAudioToWave(byte[] audioBytes, WaveFileWriter waveWriter)
    {
        using var ms = new MemoryStream(audioBytes);
        using var reader = new WaveFileReader(ms);

        if (!reader.WaveFormat.Equals(waveWriter.WaveFormat))
        {
            using var resampler = new MediaFoundationResampler(reader, waveWriter.WaveFormat);
            resampler.ResamplerQuality = 60;
            WaveFileWriter.WriteWavFileToStream(waveWriter, resampler);
        }
        else
        {
            reader.CopyTo(waveWriter);
        }
    }
}