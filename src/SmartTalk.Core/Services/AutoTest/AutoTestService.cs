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
            AudioOptions = new ChatAudioOptions(ChatOutputAudioVoice.Alloy, ChatOutputAudioFormat.Wav)
        };

        var wavFiles = new List<string>();

        try
        {
            foreach (var userMp3 in customerMp3List)
            {
                if (userMp3 == null || userMp3.Length == 0)
                    continue;

                // 转换 mp3 -> wav（统一采样率和单声道）
                var wavFile = Path.GetTempFileName() + ".wav";
                ConvertMp3ToUniformWav(userMp3, wavFile);
                wavFiles.Add(wavFile);

                // 用户语音加入对话历史
                conversationHistory.Add(new UserChatMessage(
                    ChatMessageContentPart.CreateInputAudioPart(
                        BinaryData.FromBytes(await File.ReadAllBytesAsync(wavFile, cancellationToken)),
                        ChatInputAudioFormat.Wav)));

                // 调用 AI
                var completion = await client.CompleteChatAsync(conversationHistory, options, cancellationToken);

                // 保存 AI 回复 wav
                var aiWavFile = Path.GetTempFileName() + ".wav";
                await File.WriteAllBytesAsync(aiWavFile, completion.Value.OutputAudio.AudioBytes.ToArray(),
                    cancellationToken);
                wavFiles.Add(aiWavFile);

                // 保存 AI 文本，保持上下文
                conversationHistory.Add(new AssistantChatMessage(completion.Value.OutputAudio.Transcript));
            }

            // 拼接所有 wav
            var mergedWavFile = Path.GetTempFileName() + ".wav";
            MergeWavFilesToUniformFormat(wavFiles, mergedWavFile);

            return await File.ReadAllBytesAsync(mergedWavFile, cancellationToken);
        }
        finally
        {
            foreach (var f in wavFiles)
            {
                if (File.Exists(f)) File.Delete(f);
            }
        }
    }

    private void ConvertMp3ToUniformWav(byte[] mp3Bytes, string outputWavFile)
    {
        var tempMp3 = Path.GetTempFileName() + ".mp3";
        File.WriteAllBytes(tempMp3, mp3Bytes);
        var args = $"-y -i \"{tempMp3}\" -ar 16000 -ac 1 -acodec pcm_s16le \"{outputWavFile}\"";
        RunFfmpeg(args);
        File.Delete(tempMp3);
    }

    private void MergeWavFilesToUniformFormat(List<string> wavFiles, string outputFile)
    {
        if (wavFiles.Count == 0)
            throw new ArgumentException("没有 WAV 文件可合并");

        var listFile = Path.GetTempFileName();
        File.WriteAllLines(listFile, wavFiles.Select(f => $"file '{f}'"));
        var args = $"-y -f concat -safe 0 -i \"{listFile}\" -ar 16000 -ac 1 -acodec pcm_s16le \"{outputFile}\"";
        RunFfmpeg(args);
        File.Delete(listFile);
    }

    private void RunFfmpeg(string arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo)!;
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            var err = process.StandardError.ReadToEnd();
            throw new Exception($"ffmpeg 执行失败：{err}");
        }
    }
}