using System.Text;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
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

    public AutoTestProcessJobService(IFfmpegService ffmpegService, ISmartiesClient smartiesClient, ISpeechToTextService speechToTextService, IAutoTestDataProvider autoTestDataProvider, ISpeechMaticsDataProvider speechMaticsDataProvider, ISmartTalkHttpClientFactory smartTalkHttpClientFactory)
    {
        _ffmpegService = ffmpegService;
        _smartiesClient = smartiesClient;
        _speechToTextService = speechToTextService;
        _autoTestDataProvider = autoTestDataProvider;
        _speechMaticsDataProvider = speechMaticsDataProvider;
        _smartTalkHttpClientFactory = smartTalkHttpClientFactory;
    }

    public async Task HandleTestingSpeechMaticsCallBackAsync(string jobId, CancellationToken cancellationToken)
    {
        var record = await _autoTestDataProvider.GetAutoTestTaskRecordBySpeechMaticsJobIdAsync(jobId, cancellationToken).ConfigureAwait(false);

        if (record == null) return;
        
        var scenario = await _autoTestDataProvider.GetAutoTestScenarioByIdAsync(record.ScenarioId, cancellationToken).ConfigureAwait(false);
        
        var speechMaticsJob = await _speechMaticsDataProvider.GetSpeechMaticsJobAsync(jobId, cancellationToken).ConfigureAwait(false);
        
        var callBack = JsonConvert.DeserializeObject<SpeechMaticsCallBackResponseDto>(speechMaticsJob.CallbackMessage);
        
        var speakInfos = StructureDiarizationResults(callBack.Results);

        var inputJsonDto = JsonConvert.DeserializeObject<AutoTestDataItemInputJsonDto>(scenario.InputSchema);
        
        var audioContent = await _smartTalkHttpClientFactory.GetAsync<byte[]>(inputJsonDto.Recording, cancellationToken).ConfigureAwait(false);
        
        var sixSentences = speakInfos.Count > 6 ? speakInfos[..6] : speakInfos.ToList();

        var customerSpeaker = await HandlerConversationSpeakerIsCustomerAsync(sixSentences, audioContent, cancellationToken: cancellationToken);

        var customerVideos = speakInfos.Where(x => x.Speaker == customerSpeaker).ToList();
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
    
    private async Task<string> HandlerConversationSpeakerIsCustomerAsync(
        List<SpeechMaticsSpeakInfoForAutoTestDto> audioInfos, byte[] audioContent, CancellationToken cancellationToken)
    {
        var originText = "";
        
        foreach (var audioInfo in audioInfos)
        {
            originText += $"{audioInfo.Speaker}:" + await SplitAudioAsync(audioContent, audioInfo.StartTime * 1000,
                audioInfo.EndTime * 1000, TranscriptionFileType.Wav, cancellationToken).ConfigureAwait(false) + "; ";
        }

        return await CheckAudioSpeakerIsCustomerAsync(originText, cancellationToken).ConfigureAwait(false);
    }
    
    public async Task<string> SplitAudioAsync(
        byte[] file, double speakStartTimeVideo, double speakEndTimeVideo
        , TranscriptionFileType fileType = TranscriptionFileType.Wav, CancellationToken cancellationToken = default)
    {
        if (file == null) return null;

        var audioBytes = await _ffmpegService.ConvertFileFormatAsync(file, fileType, cancellationToken).ConfigureAwait(false);

        var splitAudios = await _ffmpegService.SpiltAudioAsync(audioBytes, speakStartTimeVideo, speakEndTimeVideo, cancellationToken).ConfigureAwait(false);

        var transcriptionResult = new StringBuilder();

        foreach (var reSplitAudio in splitAudios)
        {
            try
            {
                var transcriptionResponse = await _speechToTextService.SpeechToTextAsync(
                    reSplitAudio, null, TranscriptionFileType.Wav, TranscriptionResponseFormat.Text,
                    string.Empty, cancellationToken: cancellationToken).ConfigureAwait(false);

                transcriptionResult.Append(transcriptionResponse);
            }
            catch (Exception e)
            {
                Log.Warning("Audio segment transcription error: {@Exception}", e);
            }
        }

        Log.Information("Transcription result {Transcription}", transcriptionResult.ToString());

        return transcriptionResult.ToString();
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
                    Content = new CompletionsStringContent("你是一款餐厅订餐对话高度理解的智能助手，专门用于分辨那个对话角色是客户。" +
                                                           "请根据我提供的对话，判断那个角色是属于是顾客，如果S1是顾客的话，请返回\"S1\"，如果S2是顾客的话，请返回\"S2\"" +
                                                           "- 样本与输出：\n" +
                                                           "S1:你好,江南春; S2:你好, 我可以订餐吗; S1:请问你需要什么; S2:我要一份咖喱牛腩; S1:好的，还有什么需要的; S2:没有了，什么时候取餐; output:S2\n" +
                                                           "S1:你好，请问是XX餐厅吗; S2:您好，是的，这里是XX餐厅，请问需要点什么; S1:我要外卖一份麻婆豆腐; S2:好的，地址是？; S1:XX小区3号楼; S2:好的，马上安排; output:S1\n" +
                                                           "S1:您好，这里是香满楼餐厅; S2:你好，我想取消刚刚的订单; S1:请问是哪个订单; S2:电话尾号8888; S1:收到，已为您取消; output:S2\n" +
                                                           "S1:喂，请问还有位置吗; S2:您好，我们今天晚上客满了; S1:好的，谢谢; output:S1\n" +
                                                           "S1:你好，我要订餐; S2:您好，请问要点些什么; S1:一份宫保鸡丁套餐; S2:好的，请稍等; output:S2")
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
}