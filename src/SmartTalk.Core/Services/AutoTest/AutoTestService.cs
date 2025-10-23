using AutoMapper;
using NAudio.Wave;
using OpenAI.Chat;
using SmartTalk.Core.Ioc;
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

        var allPcmSegments = new List<byte[]>();
        var client = new ChatClient("gpt-4o-audio-preview", _openAiSettings.ApiKey);

        int sampleRate = 16000;
        int bitsPerSample = 16;
        int channels = 1;

        foreach (var customerAudio in customerAudioList)
        {
            var customerWav = PcmToWav(customerAudio, sampleRate, bitsPerSample, channels);
            
            conversationHistory.Add(new UserChatMessage(ChatMessageContentPart.CreateInputAudioPart(BinaryData.FromBytes(customerWav), ChatInputAudioFormat.Wav)));
            
            var options = new ChatCompletionOptions { ResponseModalities = ChatResponseModalities.Audio, AudioOptions = new ChatAudioOptions(ChatOutputAudioVoice.Alloy, ChatOutputAudioFormat.Wav) };
            
            var completion = await client.CompleteChatAsync(conversationHistory, options, cancellationToken);

            var aiReplyText = completion.Value.Content.FirstOrDefault()?.Text;
            
            conversationHistory.Add(new AssistantChatMessage(aiReplyText));

            var aiReplyAudio = completion.Value.OutputAudio.AudioBytes.ToArray();

            allPcmSegments.Add(customerAudio);

            var aiPcm = ExtractPcmFromWav(aiReplyAudio);
            
            allPcmSegments.Add(aiPcm);
        }

        return PcmToWav(ConcatPcmSegments(allPcmSegments), sampleRate, bitsPerSample, channels);
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
}