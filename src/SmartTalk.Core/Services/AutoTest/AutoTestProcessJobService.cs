using System.Text;
using Microsoft.IdentityModel.Tokens;
using NAudio.Wave;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenAI.Chat;
using Serilog;
using Smarties.Messages.DTO.OpenAi;
using Smarties.Messages.Enums.OpenAi;
using Smarties.Messages.Requests.Ask;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.Ffmpeg;
using SmartTalk.Core.Services.Http;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Core.Services.SpeechMatics;
using SmartTalk.Core.Services.STT;
using SmartTalk.Core.Settings.OpenAi;
using SmartTalk.Messages.Dto.AutoTest;
using SmartTalk.Messages.Dto.SpeechMatics;
using TranscriptionFileType = SmartTalk.Messages.Enums.STT.TranscriptionFileType;
using TranscriptionResponseFormat = SmartTalk.Messages.Enums.STT.TranscriptionResponseFormat;

namespace SmartTalk.Core.Services.AutoTest;

public interface IAutoTestProcessJobService : IScopedDependency
{
    Task HandleTestingSpeechMaticsCallBackAsync(string jobId, CancellationToken cancellationToken);
}

public class AutoTestProcessJobService : IAutoTestProcessJobService
{
    private readonly IFfmpegService _ffmpegService;
    private readonly ISmartiesClient _smartiesClient;
    private readonly ISpeechToTextService _speechToTextService;
    private readonly IAutoTestDataProvider _autoTestDataProvider;
    private readonly ISpeechMaticsDataProvider _speechMaticsDataProvider;
    private readonly ISmartTalkHttpClientFactory _smartTalkHttpClientFactory;
    private readonly OpenAiSettings _openAiSettings;

    public AutoTestProcessJobService(IFfmpegService ffmpegService, ISmartiesClient smartiesClient, ISpeechToTextService speechToTextService, IAutoTestDataProvider autoTestDataProvider, ISpeechMaticsDataProvider speechMaticsDataProvider, ISmartTalkHttpClientFactory smartTalkHttpClientFactory
        , OpenAiSettings openAiSettings)
    {
        _ffmpegService = ffmpegService;
        _smartiesClient = smartiesClient;
        _speechToTextService = speechToTextService;
        _autoTestDataProvider = autoTestDataProvider;
        _speechMaticsDataProvider = speechMaticsDataProvider;
        _smartTalkHttpClientFactory = smartTalkHttpClientFactory;
        _openAiSettings = openAiSettings;
    }

    public async Task HandleTestingSpeechMaticsCallBackAsync(string jobId, CancellationToken cancellationToken)
    {
        var record = await _autoTestDataProvider.GetAutoTestTaskRecordBySpeechMaticsJobIdAsync(jobId, cancellationToken).ConfigureAwait(false);

        if (record == null) return;
        
        var scenario = await _autoTestDataProvider.GetAutoTestScenarioByIdAsync(record.ScenarioId, cancellationToken).ConfigureAwait(false);
        
        var speechMaticsJob = await _speechMaticsDataProvider.GetSpeechMaticsJobAsync(jobId, cancellationToken).ConfigureAwait(false);
        
        var callBack = JsonConvert.DeserializeObject<SpeechMaticsCallBackResponseDto>(speechMaticsJob.CallbackMessage);
        
        var speakInfos = StructureDiarizationResults(callBack.Results);

        var salesOrder = JsonConvert.DeserializeObject<SalesOrderDto>(scenario.InputSchema);
        
        var audioContent = await _smartTalkHttpClientFactory.GetAsync<byte[]>(salesOrder.Recording, cancellationToken).ConfigureAwait(false);
        
        var sixSentences = speakInfos.Count > 6 ? speakInfos[..6] : speakInfos.ToList();
        
        if (audioContent == null) return;
        
        var audioBytes = await _ffmpegService.ConvertFileFormatAsync(audioContent, TranscriptionFileType.Wav, cancellationToken).ConfigureAwait(false);
        
        var (customerSpeaker, audios) = await HandlerConversationSpeakerIsCustomerAsync(sixSentences, audioBytes, cancellationToken: cancellationToken).ConfigureAwait(false);

        var customerAudioInfos = speakInfos.Where(x => x.Speaker == customerSpeaker && !sixSentences.Any(s => s.StartTime == x.StartTime)).ToList();

        foreach (var audioInfo in customerAudioInfos)
        {
            audioInfo.Audio = await _ffmpegService.SpiltAudioAsync(audioBytes, audioInfo.StartTime * 1000, audioInfo.EndTime * 1000, cancellationToken).ConfigureAwait(false);
        }
        
        customerAudioInfos.AddRange(audios);

        var customerAudios = customerAudioInfos.OrderBy(x => x.StartTime).Select(x => x.Audio).ToList();

        var jObject = JObject.Parse(record.InputSnapshot);

        string promptDesc = jObject["PromptText"]?["desc"]?.ToString();
        
        var conversationAudios = await ProcessAudioConversationAsync(customerAudios, promptDesc, cancellationToken).ConfigureAwait(false);
    }
    
    private List<SpeechMaticsSpeakInfoForAutoTestDto> StructureDiarizationResults(List<SpeechMaticsResultDto> results)
    {
        string currentSpeaker = null;
        var startTime = 0.0;
        var endTime = 0.0;
        var speakInfos = new List<SpeechMaticsSpeakInfoForAutoTestDto>();

        foreach (var result in results.Where(result => !result.Alternatives.IsNullOrEmpty()))
        {
            if (currentSpeaker == null)
            {
                currentSpeaker = result.Alternatives[0].Speaker;
                startTime = result.StartTime;
                endTime = result.EndTime;
                continue;
            }

            if (result.Alternatives[0].Speaker.Equals(currentSpeaker))
            {
                endTime = result.EndTime;
            }
            else
            {
                speakInfos.Add(new SpeechMaticsSpeakInfoForAutoTestDto { EndTime = endTime, StartTime = startTime, Speaker = currentSpeaker });
                currentSpeaker = result.Alternatives[0].Speaker;
                startTime = result.StartTime;
                endTime = result.EndTime;
            }
        }

        speakInfos.Add(new SpeechMaticsSpeakInfoForAutoTestDto { EndTime = endTime, StartTime = startTime, Speaker = currentSpeaker });

        Log.Information("Structure diarization results : {@speakInfos}", speakInfos);
        
        return speakInfos;
    }
    
    private async Task<(string, List<SpeechMaticsSpeakInfoForAutoTestDto>)> HandlerConversationSpeakerIsCustomerAsync(
        List<SpeechMaticsSpeakInfoForAutoTestDto> audioInfos, byte[] audioBytes, CancellationToken cancellationToken)
    {
        var originText = "";
     
        foreach (var audioInfo in audioInfos)
        {
            var (audioText, audio) = await SplitAudioAsync(
                    audioBytes, audioInfo.StartTime * 1000, audioInfo.EndTime * 1000, cancellationToken).ConfigureAwait(false);
          
            originText += $"{audioInfo.Speaker}:" + audioText + "; ";

            audioInfo.Audio = audio;
        }

        var speaker = await CheckAudioSpeakerIsCustomerAsync(originText, cancellationToken).ConfigureAwait(false);
        
        return (speaker, audioInfos.Where(x => x.Speaker == speaker).OrderBy(x => x.StartTime).ToList());
    }
    
    private async Task<(string, byte[])> SplitAudioAsync(byte[] audioBytes, double speakStartTimeVideo, double speakEndTimeVideo, CancellationToken cancellationToken = default)
    {
        var splitAudios = await _ffmpegService.SpiltAudioAsync(audioBytes, speakStartTimeVideo, speakEndTimeVideo, cancellationToken).ConfigureAwait(false);

        var transcriptionResult = new StringBuilder();
        
        try
        {
            var transcriptionResponse = await _speechToTextService.SpeechToTextAsync(
                splitAudios, null, TranscriptionFileType.Wav, TranscriptionResponseFormat.Text,
                string.Empty, cancellationToken: cancellationToken).ConfigureAwait(false); 
            
            transcriptionResult.Append(transcriptionResponse); 
        }
        catch (Exception e)
        {
            Log.Warning("Audio segment transcription error: {@Exception}", e);
        }

        Log.Information("Transcription result {Transcription}", transcriptionResult.ToString());

        return (transcriptionResult.ToString(), splitAudios);
    }
    
    private async Task<string> CheckAudioSpeakerIsCustomerAsync(string query, CancellationToken cancellationToken)
    {
        var completionResult = await _smartiesClient.PerformQueryAsync(new AskGptRequest
        {
            Messages = new List<CompletionsRequestMessageDto>
            {
                new()
                {
                    Role = "system",
                    Content = new CompletionsStringContent("你是一款销售与餐厅老板对话高度理解的智能助手，专门用于分辨那个对话角色是餐厅老板。" +
                                                           "请根据我提供的对话，判断那个角色是属于是餐厅老板，如果S1是餐厅老板的话，请返回\"S1\"，如果S2是餐厅老板的话，请返回\"S2\"" +
                                                           "- 样本与输出：\n" +
                                                           "S1:你好，今天我要订货; S2: 今日想订些什么 S1:一箱西兰花 S2: 好的 output:S1\n" +
                                                           "S1: 老板您好，我们今天有新到的牛腩; S2: 嗯，给我留三斤; S1: 没问题; output:S2\n" +
                                                           "S1: 最近土豆怎么样，质量好吗; S2: 很好，新货刚到; S1: 那给我两箱; S2: 好的; output:S1\n" +
                                                           "S1: 我这边的猪肉库存不多了; S2: 那我给您安排两箱明早送; S1: 可以，谢谢; output:S1\n" +
                                                           "S1: 老板，您这周还要西红柿吗; S2: 要的，送十箱; S1: 好的; output:S2")
                },
                new()
                {
                    Role = "user",
                    Content = new CompletionsStringContent($"input: {query}, output:")
                }
            },
            Model = OpenAiModel.Gpt4o
        }, cancellationToken).ConfigureAwait(false);

        return completionResult.Data.Response;
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
            if (userPcm == null || userPcm.Length == 0) continue;

            combinedStream.Write(userPcm, 0, userPcm.Length);

            var userWav = PcmToWav(userPcm, 16000, 16, 1);

            conversationHistory.Add(new UserChatMessage(
                ChatMessageContentPart.CreateInputAudioPart(BinaryData.FromBytes(userWav), ChatInputAudioFormat.Wav)
            ));

            var completion = await client.CompleteChatAsync(conversationHistory, options, cancellationToken);
            var aiPcm = completion.Value.OutputAudio.AudioBytes.ToArray();

            combinedStream.Write(aiPcm, 0, aiPcm.Length);

            conversationHistory.Add(new AssistantChatMessage(completion.Value.OutputAudio.Transcript));
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