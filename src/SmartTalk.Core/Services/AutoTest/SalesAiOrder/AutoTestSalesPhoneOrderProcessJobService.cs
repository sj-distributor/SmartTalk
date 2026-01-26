using System.ClientModel;
using System.Buffers;
using Serilog;
using System.Text;
using OpenAI.Chat;
using Newtonsoft.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using AutoMapper;
using DocumentFormat.OpenXml.Drawing;
using SmartTalk.Core.Ioc;
using Google.Cloud.Translation.V2;
using Hangfire;
using SmartTalk.Core.Services.STT;
using SmartTalk.Core.Services.Http;
using Smarties.Messages.DTO.OpenAi;
using Microsoft.IdentityModel.Tokens;
using NAudio.Wave;
using Smarties.Messages.Enums.OpenAi;
using SmartTalk.Core.Settings.OpenAi;
using Smarties.Messages.Requests.Ask;
using SmartTalk.Core.Domain.AutoTest;
using SmartTalk.Core.Domain.SpeechMatics;
using SmartTalk.Core.Services.Ffmpeg;
using SmartTalk.Messages.Dto.AutoTest;
using SmartTalk.Messages.Enums.Caching;
using SmartTalk.Messages.Enums.AutoTest;
using SmartTalk.Messages.Dto.SpeechMatics;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Core.Services.SpeechMatics;
using SmartTalk.Core.Services.Caching.Redis;
using SmartTalk.Messages.Enums.SpeechMatics;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Core.Services.Attachments;
using SmartTalk.Core.Services.Jobs;
using SmartTalk.Core.Services.Sale;
using SmartTalk.Messages.Commands.Attachments;
using SmartTalk.Messages.Dto.Attachments;
using SmartTalk.Messages.Dto.RingCentral;
using SmartTalk.Messages.Dto.Sales;
using SmartTalk.Messages.Requests.AutoTest;
using JsonSerializer = System.Text.Json.JsonSerializer;
using Path = System.IO.Path;
using TranscriptionFileType = SmartTalk.Messages.Enums.STT.TranscriptionFileType;
using TranscriptionResponseFormat = SmartTalk.Messages.Enums.STT.TranscriptionResponseFormat;

namespace SmartTalk.Core.Services.AutoTest.SalesAiOrder;

public interface IAutoTestSalesPhoneOrderProcessJobService : IScopedDependency
{
    Task StartTestingSalesPhoneOrderTaskAsync(int taskId, int taskRecordId, CancellationToken cancellationToken);
    
    Task HandleTestingSalesPhoneOrderSpeechMaticsCallBackAsync(string jobId, CancellationToken cancellationToken);

    Task ProcessPartialRecordingOrderMatchingAsync(
        int scenarioId, int dataSetId, int recordId, DateTime from, DateTime to, string customerId, CancellationToken cancellationToken);
}

public class AutoTestSalesPhoneOrderProcessJobService : IAutoTestSalesPhoneOrderProcessJobService
{
    private readonly IMapper _mapper;
    private readonly ICrmClient _crmClient;
    private readonly ISalesClient _salesClient;
    private readonly IFfmpegService _ffmpegService;
    private readonly OpenAiSettings _openAiSettings;
    private readonly ISmartiesClient _smartiesClient;
    private readonly IRedisSafeRunner _redisSafeRunner;
    private readonly TranslationClient _translationClient;
    private readonly ISapGatewayClients _sapGatewayClient;
    private readonly IRingCentralClient _ringCentralClient;
    private readonly IAttachmentService _attachmentService;
    private readonly ISalesDataProvider _salesDataProvider;
    private readonly ISpeechToTextService _speechToTextService;
    private readonly ISpeechMaticsService _speechMaticsService;
    private readonly IAutoTestDataProvider _autoTestDataProvider;
    private readonly ISmartTalkHttpClientFactory _httpClientFactory;
    private readonly ISpeechMaticsDataProvider _speechMaticsDataProvider;
    private readonly ISmartTalkHttpClientFactory _smartTalkHttpClientFactory;
    private readonly ISmartTalkBackgroundJobClient _smartTalkBackgroundJobClient;
    private readonly IAiSpeechAssistantDataProvider _aiSpeechAssistantDataProvider;

    public AutoTestSalesPhoneOrderProcessJobService(
        IMapper mapper,
        ICrmClient crmClient,
        ISalesClient salesClient,
        IFfmpegService ffmpegService,
        OpenAiSettings openAiSettings,
        ISmartiesClient smartiesClient,
        IRedisSafeRunner redisSafeRunner,
        TranslationClient translationClient,
        ISapGatewayClients sapGatewayClient,
        IRingCentralClient ringCentralClient,
        IAttachmentService attachmentService,
        ISalesDataProvider salesDataProvider,
        ISpeechMaticsService speechMaticsService,
        ISpeechToTextService speechToTextService,
        IAutoTestDataProvider autoTestDataProvider,
        ISmartTalkHttpClientFactory httpClientFactory,
        ISpeechMaticsDataProvider speechMaticsDataProvider,
        ISmartTalkHttpClientFactory smartTalkHttpClientFactory,
        ISmartTalkBackgroundJobClient smartTalkBackgroundJobClient,
        IAiSpeechAssistantDataProvider aiSpeechAssistantDataProvider)
    {
        _mapper = mapper;
        _crmClient = crmClient;
        _salesClient = salesClient;
        _ffmpegService = ffmpegService;
        _openAiSettings = openAiSettings;
        _smartiesClient = smartiesClient;
        _redisSafeRunner = redisSafeRunner;
        _attachmentService = attachmentService;
        _salesDataProvider = salesDataProvider;
        _translationClient = translationClient;
        _sapGatewayClient = sapGatewayClient;
        _ringCentralClient = ringCentralClient;
        _httpClientFactory = httpClientFactory;
        _speechToTextService = speechToTextService;
        _speechMaticsService = speechMaticsService;
        _autoTestDataProvider = autoTestDataProvider;
        _speechMaticsDataProvider = speechMaticsDataProvider;
        _smartTalkHttpClientFactory = smartTalkHttpClientFactory;
        _smartTalkBackgroundJobClient = smartTalkBackgroundJobClient;
        _aiSpeechAssistantDataProvider = aiSpeechAssistantDataProvider;
    }

    public async Task StartTestingSalesPhoneOrderTaskAsync(int taskId, int taskRecordId, CancellationToken cancellationToken)
    {
        var task = await _autoTestDataProvider.GetAutoTestTaskByIdAsync(taskId, cancellationToken).ConfigureAwait(false);
        var record = await _autoTestDataProvider.GetTestTaskRecordsByIdAsync(taskRecordId, cancellationToken).ConfigureAwait(false);
        
        Log.Information("Start Testing SalesPhoneOrder {@taskId}, {@taskRecord}", task, record);

        if (task == null || record == null) return;

        var inputJsonSnapshot = JsonConvert.DeserializeObject<AutoTestInputJsonDto>(record.InputSnapshot);
        var recordingContent = await _httpClientFactory.GetAsync<byte[]>(inputJsonSnapshot.Recording, cancellationToken).ConfigureAwait(false);
        
        if (recordingContent == null) return;
        
        var transcription = await _speechToTextService.SpeechToTextAsync(
            recordingContent, fileType: TranscriptionFileType.Wav, responseFormat: TranscriptionResponseFormat.Text, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        var detection = await _translationClient.DetectLanguageAsync(transcription, cancellationToken).ConfigureAwait(false);
        
        record.SpeechMaticsJobId = await _speechMaticsService.CreateSpeechMaticsJobAsync(recordingContent, Guid.NewGuid().ToString("N") + ".wav", detection.Language, SpeechMaticsJobScenario.TestingSalesPhoneOrder, cancellationToken).ConfigureAwait(false);
        Log.Information("Created speech matics job, id {RecordSpeechMaticsJobId}", record.SpeechMaticsJobId);
        
        await _autoTestDataProvider.UpdateAutoTestTaskRecordAsync(record, true, cancellationToken).ConfigureAwait(false);
    }

    public async Task HandleTestingSalesPhoneOrderSpeechMaticsCallBackAsync(string jobId, CancellationToken cancellationToken)
    {
        var (scenario, task, record, assistant, speechMaticsJob) = await CollectAutoTestDataByJobIdAsync(jobId, cancellationToken);
        if (scenario == null || task == null || record == null || assistant == null || speechMaticsJob == null) return;
        
        await ProcessingTestSalesPhoneOrderSpeechMaticsCallBackAsync(record, assistant, speechMaticsJob, cancellationToken).ConfigureAwait(false);
        
        Log.Information("Processed record : {@Record}", record);
        
        await _autoTestDataProvider.UpdateAutoTestTaskRecordAsync(record, true, cancellationToken).ConfigureAwait(false);
        
        await HandleAutoTestTaskStatusChangeAsync(task, cancellationToken).ConfigureAwait(false);
    }

    public async Task ProcessPartialRecordingOrderMatchingAsync(int scenarioId, int dataSetId, int recordId, DateTime from, DateTime to, string customerId, CancellationToken cancellationToken)
    {
        var record = await _autoTestDataProvider.GetAutoTestImportDataRecordAsync(recordId, cancellationToken).ConfigureAwait(false);

        try
        {
            var contacts = await _crmClient.GetCustomerContactsAsync(customerId.ToString(), cancellationToken).ConfigureAwait(false);
            var phoneNumbers = contacts.Select(c => NormalizePhone(c.Phone)).Where(p => !string.IsNullOrEmpty(p)).Distinct().ToList();
            Log.Information("Normalized phone numbers: {@PhoneNumbers}", phoneNumbers);

            if (!phoneNumbers.Any()) return;
            
            var callRecords = (await _autoTestDataProvider.GetCallRecordsByPhonesAndRangeAsync(phoneNumbers, from, to, cancellationToken).ConfigureAwait(false)).OrderBy(r => r.StartTimeUtc).ToList();

            if (!callRecords.Any())
            {
                Log.Information("Scenario {ScenarioId} 时间段 {From}~{To} 没有通话数据", scenarioId, from, to);
                return;
            }

            var pstZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
            var singleDayRecords = callRecords.SelectMany(r => new[]
                {
                    new { Phone = NormalizePhone(r.FromNumber), Record = r }, 
                    new { Phone = NormalizePhone(r.ToNumber), Record = r }
                }).Where(x => !string.IsNullOrEmpty(x.Phone)).GroupBy(x => new
                {
                    Phone = x.Phone, Date = TimeZoneInfo.ConvertTimeFromUtc(x.Record.StartTimeUtc, pstZone).Date
                }).Where(g => g.Count() == 1).Select(g => g.First().Record).Distinct().OrderBy(r => r.StartTimeUtc).ToList();

            if (!singleDayRecords.Any())
            {
                Log.Information("Scenario {ScenarioId} 没有任何『一天只有一条』的录音", scenarioId);
                return;
            }

            var matchTasks = singleDayRecords.Select(callRecord => MatchOrderAndRecordingAsync(customerId, callRecord, scenarioId, recordId, cancellationToken)).ToList();
            
            var autoTestDataItems = (await Task.WhenAll(matchTasks)).Where(x => x != null).ToList();
            
            var sortedAutoTestDataItems = autoTestDataItems.Select(x => new { 
                    Item = x,
                    OrderDate = JsonSerializer.Deserialize<AutoTestInputJsonDto>(x.InputJson)?.OrderDate })
                .OrderBy(x => x.OrderDate)
                .Select(x => x.Item)
                .ToList();
            
            if (sortedAutoTestDataItems.Any())
            {
                await _autoTestDataProvider.AddAutoTestDataItemsAsync(sortedAutoTestDataItems, true, cancellationToken).ConfigureAwait(false);

                var autoTestDataSetItems = sortedAutoTestDataItems.Select(x => new AutoTestDataSetItem
                {
                    DataSetId = dataSetId,
                    DataItemId = x.Id,
                }).ToList();
                
                await _autoTestDataProvider.AddAutoTestDataSetItemsAsync(autoTestDataSetItems, cancellationToken).ConfigureAwait(false);
            }
            else Log.Information("Scenario {ScenarioId} 没有匹配的记录", scenarioId);

            record.Status = AutoTestStatus.Done;
            await _autoTestDataProvider.UpdateAutoTestImportRecordAsync(record, true, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ProcessSingleMonthAsync 失败 ScenarioId={ScenarioId}", scenarioId);
        }
    }
    
    private async Task ProcessingTestSalesPhoneOrderSpeechMaticsCallBackAsync(
        AutoTestTaskRecord record, Domain.AISpeechAssistant.AiSpeechAssistant assistant, SpeechMaticsJob speechMaticsJob, CancellationToken cancellationToken)
    {
        try
        {
            var audioBytes = await FetchingRecordAudioAsync(record, cancellationToken).ConfigureAwait(false);
            
            Log.Information("FetchingRecordAudioAsync audioBytes: {@audioBytes}", audioBytes);
              
            if (audioBytes == null) record.Status = AutoTestTaskRecordStatus.Failed;

            var customerAudios = await ExtractingCustomerAudioAsync(speechMaticsJob, audioBytes, cancellationToken).ConfigureAwait(false);
            if (customerAudios == null || customerAudios.Count == 0) record.Status = AutoTestTaskRecordStatus.Failed;
 
            var conversationAudios = await ProcessAudioConversationAsync(customerAudios, assistant, cancellationToken).ConfigureAwait(false);
            
            Log.Information("ProcessAudioConversationAsync conversationAudios: {@conversationAudios}", conversationAudios);
            
            if (conversationAudios == null || conversationAudios.Length == 0) record.Status = AutoTestTaskRecordStatus.Failed;
        
            var (report, aiOrder) = await GenerateSalesAiOrderAsync(assistant, conversationAudios, cancellationToken).ConfigureAwait(false);

            Log.Information("GenerateSalesAiOrderAsync report: {@report}, aiOrder: {@aiOrder}", report, aiOrder);
            
            var inputSnapshot = JsonConvert.DeserializeObject<AutoTestInputJsonDto>(record.InputSnapshot);
            var comparedAiOrderItems = AutoTestOrderCompare(inputSnapshot.Detail, aiOrder);
            var normalizedOutput = await HandleAutoTestNormalizedOutput(conversationAudios, report, inputSnapshot.Detail, comparedAiOrderItems, cancellationToken).ConfigureAwait(false);

            record.Status = AutoTestTaskRecordStatus.Done;
            record.NormalizedOutput = normalizedOutput;
        }
        catch (Exception e)
        {
            Log.Information("ProcessingTestSalesPhoneOrderSpeechMaticsCallBackAsync Exception: {Exception}", e);
            record.Status = AutoTestTaskRecordStatus.Failed;            
        }
    }

    private async Task<(AutoTestScenario, AutoTestTask, AutoTestTaskRecord, Domain.AISpeechAssistant.AiSpeechAssistant, SpeechMaticsJob)> CollectAutoTestDataByJobIdAsync(string jobId, CancellationToken cancellationToken)
    {
        var record = await _autoTestDataProvider.GetAutoTestTaskRecordBySpeechMaticsJobIdAsync(jobId, cancellationToken).ConfigureAwait(false);
        
        Log.Information("Handling auto test task record: {@Record}", record);

        if (record == null) return (null, null, null, null, null);
        
        var task  = await _autoTestDataProvider.GetAutoTestTaskByIdAsync(record.TestTaskId, cancellationToken).ConfigureAwait(false);
        
        if (task == null) return (null, null, null, null, null);
        
        var scenario = await _autoTestDataProvider.GetAutoTestScenarioByIdAsync(record.ScenarioId, cancellationToken).ConfigureAwait(false);
        
        var speechMaticsJob = await _speechMaticsDataProvider.GetSpeechMaticsJobAsync(jobId, cancellationToken).ConfigureAwait(false);
        
        Log.Information("Related scenario: {@Scenario}, task: {@Task}, speechmatics job:{@SpeechMaticsJob}", scenario, task, speechMaticsJob);
        
        var taskParams = JsonConvert.DeserializeObject<AutoTestTaskParamsDto>(task.Params);

        var assistant = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantByIdAsync(taskParams.AssistantId, cancellationToken).ConfigureAwait(false);

        return (scenario, task, record, assistant, speechMaticsJob);
    }

    private async Task<byte[]> FetchingRecordAudioAsync(AutoTestTaskRecord record, CancellationToken cancellationToken)
    {
        string recording = null;
        
        using (var doc = JsonDocument.Parse(record.InputSnapshot))
        {
            var root = doc.RootElement;

            if (root.TryGetProperty("Recording", out var recordingElement))
            {
                recording = recordingElement.GetString();
            }
        }
        Log.Information("FetchingRecordAudioAsync recording: {@recording}", recording);
        
        if (recording == null) return null;
        
        var audioContent = await _smartTalkHttpClientFactory.GetAsync<byte[]>(recording, cancellationToken).ConfigureAwait(false);
        
        Log.Information("FetchingRecordAudioAsync audioContent: {@audioContent}", audioContent);
        
        if (audioContent == null) return null;
        
        return await _ffmpegService.Convert8KHzWavTo24KHzWavAsync(audioContent, cancellationToken).ConfigureAwait(false);
    }

    private async Task<List<byte[]>> ExtractingCustomerAudioAsync(SpeechMaticsJob speechMaticsJob, byte[] audioBytes, CancellationToken cancellationToken)
    {
        var callBack = JsonConvert.DeserializeObject<SpeechMaticsCallBackResponseDto>(speechMaticsJob.CallbackMessage);
        
        var speakInfos = StructureDiarizationResults(callBack.Results);
        
        var sixSentences = speakInfos.Count > 6 ? speakInfos[..6] : speakInfos.ToList();
        
        var (customerSpeaker, audios) = await HandlerConversationSpeakerIsCustomerAsync(sixSentences, audioBytes, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        var customerAudioInfos = speakInfos.Where(x => x.Speaker == customerSpeaker && !sixSentences.Any(s => Math.Abs(s.StartTime - x.StartTime) == 0)).ToList();
        
        foreach (var audioInfo in customerAudioInfos)
        {
            audioInfo.Audio = await _ffmpegService.SpiltAudioAsync(audioBytes, audioInfo.StartTime * 1000, audioInfo.EndTime * 1000, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        customerAudioInfos.AddRange(audios);
        var customerAudios = customerAudioInfos.OrderBy(x => x.StartTime).Select(x => x.Audio).ToList();

        Log.Information("Extracted customer audio, audio count: {Count}", customerAudios.Count);
        
        return customerAudios;
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

        if (speaker != "S1" && speaker != "S2")
            speaker = "S1";
        
        return (speaker, audioInfos.Where(x => x.Speaker == speaker).OrderBy(x => x.StartTime).ToList());
    }
    
    private async Task<(string, byte[])> SplitAudioAsync(byte[] audioBytes, double speakStartTimeVideo, double speakEndTimeVideo, CancellationToken cancellationToken = default)
    {
        var splitAudios = await _ffmpegService.SpiltAudioAsync(audioBytes, speakStartTimeVideo, speakEndTimeVideo, cancellationToken: cancellationToken).ConfigureAwait(false);

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
                                                           "请根据我提供的对话，判断那个角色是属于是餐厅老板。" +
                                                           "输出规则:" +
                                                           "1.如果S1是餐厅老板的话，请返回\"S1\"" +
                                                           "2.如果S2是餐厅老板的话，请返回\"S2\" " +
                                                           "3.如果根据对话无法确定餐厅老板身份，请默认返回 \"S1\"" +
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

    private async Task<byte[]> ProcessAudioConversationAsync(List<byte[]> customerWavList, Domain.AISpeechAssistant.AiSpeechAssistant assistant, CancellationToken cancellationToken)
    {
        var prompt = await BuildConversationPromptAsync(assistant, cancellationToken).ConfigureAwait(false);

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
            foreach (var wavBytes in customerWavList)
            {
                if (wavBytes == null || wavBytes.Length == 0)
                    continue;

                var userWavFile = Path.GetTempFileName() + ".wav";
                await File.WriteAllBytesAsync(userWavFile, wavBytes, cancellationToken);
                wavFiles.Add(userWavFile);

                conversationHistory.Add(new UserChatMessage(
                    ChatMessageContentPart.CreateInputAudioPart(
                        BinaryData.FromBytes(await File.ReadAllBytesAsync(userWavFile, cancellationToken)),
                        ChatInputAudioFormat.Wav)));
                
                ClientResult<ChatCompletion> completion = null;

                try
                {
                    completion = await RetryWithDelayAsync(
                        async ct => await client.CompleteChatAsync(conversationHistory, options, ct),
                        result => result?.Value?.OutputAudio?.AudioBytes == null || result.Value.OutputAudio.AudioBytes.Length == 0,
                        maxRetryCount: 3,
                        delay: TimeSpan.FromMilliseconds(500),
                        cancellationToken: cancellationToken
                    );
                }
                catch (Exception ex)
                {
                    Log.Information($"Warning: AI audio generation failed for one input: {ex.Message}");
                }

                if (completion?.Value?.OutputAudio?.AudioBytes is { Length: > 0 })
                {
                    var aiWavFile = Path.GetTempFileName() + ".wav";
                    await File.WriteAllBytesAsync(aiWavFile, completion.Value.OutputAudio.AudioBytes.ToArray(), cancellationToken);
                    wavFiles.Add(aiWavFile);

                    conversationHistory.Add(new AssistantChatMessage(completion.Value.OutputAudio.Id));
                }
                else
                {
                    conversationHistory.Add(new AssistantChatMessage(completion?.Value?.OutputAudio?.Transcript ?? string.Empty));

                    Log.Information("Warning: Skipped one audio input due to repeated failures.");
                }
            }

            var mergedWavFile = Path.GetTempFileName() + ".wav";
            await _ffmpegService.MergeWavFilesToUniformFormat(wavFiles, mergedWavFile, cancellationToken);

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

    private async Task<(string Report, List<AutoTestInputDetail> Order)> GenerateSalesAiOrderAsync(Domain.AISpeechAssistant.AiSpeechAssistant assistant, byte[] audio, CancellationToken cancellationToken)
    {
        var messages = await ConfigureRecordAnalyzePromptAsync(assistant, audio, cancellationToken).ConfigureAwait(false);
        
        ChatClient client = new("gpt-4o-audio-preview", _openAiSettings.ApiKey);

        ChatCompletionOptions options = new() { ResponseModalities = ChatResponseModalities.Text };

        ChatCompletion completion = await client.CompleteChatAsync(messages, options, cancellationToken);
        var report = completion.Content.FirstOrDefault()?.Text;
        Log.Information("sales record analyze report:" + report);

        var soldToIds = new List<string>();
        if (!string.IsNullOrEmpty(assistant.Name)) soldToIds = assistant.Name.Split('/', StringSplitOptions.RemoveEmptyEntries).ToList();
        var historyItems = await GetCustomerHistoryItemsBySoldToIdAsync(soldToIds, cancellationToken);

        var originOrder = await ExtractAndMatchOrderItemsFromReportAsync(report, historyItems, cancellationToken);

        var order = _mapper.Map<List<AutoTestInputDetail>>(originOrder.SelectMany(x => x.Orders).ToList());
        Log.Information("sales ai order: {@Order}", order);
        
        return (report, order);
    }
    
    private async Task<List<ChatMessage>> ConfigureRecordAnalyzePromptAsync(
        Domain.AISpeechAssistant.AiSpeechAssistant aiSpeechAssistant, byte[] audioContent, CancellationToken cancellationToken)
    {
        var pstTime = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow,
            TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time"));
        var currentTime = pstTime.ToString("yyyy-MM-dd HH:mm:ss");
        
        var soldToIds = !string.IsNullOrEmpty(aiSpeechAssistant.Name)
            ? aiSpeechAssistant.Name.Split('/', StringSplitOptions.RemoveEmptyEntries).ToList()
            : new List<string>();

        var customerItemsCacheList = await _salesDataProvider.GetCustomerItemsCacheBySoldToIdsAsync(soldToIds, cancellationToken);
        var customerItemsString = string.Join(Environment.NewLine, soldToIds.Select(id => customerItemsCacheList.FirstOrDefault(c => c.Filter == id)?.CacheValue ?? ""));

        var audioData = BinaryData.FromBytes(audioContent);
        List<ChatMessage> messages =
        [
            new SystemChatMessage(( aiSpeechAssistant.CustomRecordAnalyzePrompt).Replace("#{call_from}", "").Replace("#{current_time}", currentTime ?? "").Replace("#{customer_items}", customerItemsString)),
            new UserChatMessage(ChatMessageContentPart.CreateInputAudioPart(audioData, ChatInputAudioFormat.Wav)),
            new UserChatMessage("幫我根據錄音生成分析報告：")
        ];

        return messages;
    }
    
    private async Task<List<(string Material, string MaterialDesc, DateTime? InvoiceDate)>> GetCustomerHistoryItemsBySoldToIdAsync(List<string> soldToIds, CancellationToken cancellationToken)
    {
        List<(string Material, string MaterialDesc, DateTime? InvoiceDate)> historyItems = [];

        var askInfoResponse = await _salesClient
            .GetAskInfoDetailListByCustomerAsync(
                new GetAskInfoDetailListByCustomerRequestDto { CustomerNumbers = soldToIds }, cancellationToken)
            .ConfigureAwait(false);
        var orderHistoryResponse = await _salesClient
            .GetOrderHistoryByCustomerAsync(
                new GetOrderHistoryByCustomerRequestDto { CustomerNumber = soldToIds.FirstOrDefault() },
                cancellationToken).ConfigureAwait(false);

        if (askInfoResponse?.Data != null && askInfoResponse.Data.Any())
            historyItems.AddRange(askInfoResponse.Data.Where(x => !string.IsNullOrWhiteSpace(x.Material))
                .Select(x => (x.Material, x.MaterialDesc, (DateTime?)null)));

        if (orderHistoryResponse?.Data != null && orderHistoryResponse.Data.Any())
            historyItems.AddRange(
                orderHistoryResponse?.Data.Where(x => !string.IsNullOrWhiteSpace(x.MaterialNumber))
                    .Select(x => (x.MaterialNumber, x.MaterialDescription, x.LastInvoiceDate)) ??
                new List<(string, string, DateTime?)>());

        return historyItems;
    }
    
    private async Task<List<ExtractedOrderDto>> ExtractAndMatchOrderItemsFromReportAsync(string reportText, List<(string Material, string MaterialDesc, DateTime? invoiceDate)> historyItems, CancellationToken cancellationToken)
    {
        var client = new ChatClient("gpt-4.1", _openAiSettings.ApiKey);

        var materialListText = string.Join("\n",
            historyItems.Select(x => $"{x.MaterialDesc} ({x.Material})【{x.invoiceDate}】"));

        var systemPrompt =
            "你是一名訂單分析助手。請從下面的客戶分析報告文字中提取所有下單的物料名稱、數量、單位，並且用歷史物料列表盡力匹配每個物料的materialNumber。" +
            "如果報告中提到了預約送貨時間，請提取送貨時間（格式yyyy-MM-dd）。" +
            "如果客戶提到了分店名，請提取 StoreName；如果提到第幾家店，請提取 StoreNumber。\n" +
            "請嚴格傳回一個 JSON 對象，頂層字段為 \"stores\"，每个店铺对象包含：StoreName（可空字符串）, StoreNumber（可空字符串）, DeliveryDate（可空字符串），orders（数组，元素包含 name, quantity, unit, materialNumber, deliveryDate）。\n" +
            "範例：\n" +
            "{\n    \"stores\": [\n        {\n            \"StoreName\": \"HaiDiLao\",\n            \"StoreNumber\": \"1\",\n            \"DeliveryDate\": \"2025-08-20\",\n            \"orders\": [\n                {\n                    \"name\": \"雞胸肉\",\n                    \"quantity\": 1,\n                    \"unit\": \"箱\",\n                    \"materialNumber\": \"000000000010010253\"\n                }\n            ]\n        }\n    ]\n}" +
            "歷史物料列表：\n" + materialListText + "\n\n" +
            "每個物料的格式為「物料名稱（物料號碼）」，部分物料會包含日期\n 當有多個相似的物料名稱時，請根據以下規則選擇匹配的物料號碼：1. **優先選擇沒有日期的物料。**\n 2. 如果所有相似物料都有日期，請選擇日期**最新** 的那個物料。\n\n  " +
            "注意：\n1. 必須嚴格輸出 JSON，物件頂層字段必須是 \"stores\"，不要有其他字段或額外說明。\n2. 提取的物料名稱需要為繁體中文。\n3. 如果没有提到店铺信息，但是有下单内容，则StoreName和StoreNumber可为空值，orders要正常提取。\n4. **如果客戶分析文本中沒有任何可識別的下單信息，請返回：{ \"stores\": [] }。不得臆造或猜測物料。** \n" +
            "請務必完整提取報告中每一個提到的物料";
        Log.Information("Sending prompt to GPT: {Prompt}", systemPrompt);

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage("客戶分析報告文本：\n" + reportText + "\n\n")
        };

        var completion = await client.CompleteChatAsync(messages,
            new ChatCompletionOptions
            {
                ResponseModalities = ChatResponseModalities.Text,
                ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()
            }, cancellationToken).ConfigureAwait(false);
        var jsonResponse = completion.Value.Content.FirstOrDefault()?.Text ?? "";

        Log.Information("AI JSON Response: {JsonResponse}", jsonResponse);

        try
        {
            using var jsonDoc = JsonDocument.Parse(jsonResponse);

            var storesArray = jsonDoc.RootElement.GetProperty("stores");
            var results = new List<ExtractedOrderDto>();

            foreach (var storeElement in storesArray.EnumerateArray())
            {
                var storeDto = new ExtractedOrderDto
                {
                    StoreName = storeElement.TryGetProperty("StoreName", out var sn) ? sn.GetString() ?? "" : "",
                    StoreNumber =
                        storeElement.TryGetProperty("StoreNumber", out var snum) ? snum.GetString() ?? "" : "",
                    DeliveryDate =
                        storeElement.TryGetProperty("DeliveryDate", out var dd) &&
                        DateTime.TryParse(dd.GetString(), out var dt)
                            ? DateTime.SpecifyKind(dt, DateTimeKind.Utc)
                            : DateTime.UtcNow.AddDays(1)
                };

                if (storeElement.TryGetProperty("orders", out var ordersArray))
                {
                    foreach (var orderItem in ordersArray.EnumerateArray())
                    {
                        var name = orderItem.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                        var qty = orderItem.TryGetProperty("quantity", out var q) && q.TryGetDecimal(out var dec)
                            ? dec
                            : 0;
                        var unit = orderItem.TryGetProperty("unit", out var u) ? u.GetString() ?? "" : "";
                        var materialNumber = orderItem.TryGetProperty("materialNumber", out var mn)
                            ? mn.GetString() ?? ""
                            : "";

                        materialNumber = MatchMaterialNumber(name, materialNumber, unit, historyItems);

                        storeDto.Orders.Add(new ExtractedOrderItemDto
                        {
                            Name = name,
                            Quantity = (int)qty,
                            MaterialNumber = materialNumber,
                            Unit = unit
                        });
                    }
                }

                results.Add(storeDto);
            }

            return results;
        }
        catch (Exception ex)
        {
            Log.Warning("解析GPT返回JSON失败: {Message}", ex.Message);
            return new List<ExtractedOrderDto>();
        }
    }
    
    private string MatchMaterialNumber(string itemName, string baseNumber, string unit, List<(string Material, string MaterialDesc, DateTime? invoiceDate)> historyItems)
    {
        var candidates = historyItems.Where(x => x.MaterialDesc != null && x.MaterialDesc.Contains(itemName, StringComparison.OrdinalIgnoreCase)).Select(x => x.Material).ToList();
        Log.Information("Candidate material code list: {@Candidates}", candidates);

        if (!candidates.Any()) return string.IsNullOrEmpty(baseNumber) ? "" : baseNumber;
        if (candidates.Count == 1) return candidates.First();

        var isCase = !string.IsNullOrWhiteSpace(unit) && (unit.Contains("case", StringComparison.OrdinalIgnoreCase) || unit.Contains("箱"));
        if (isCase)
        {
            var noPcList = candidates.Where(x => !x.Contains("PC", StringComparison.OrdinalIgnoreCase)).ToList();

            if (noPcList.Any())
                return noPcList.First(); 
            
            return candidates.First();
        }
        
        var pcList = candidates.Where(x => x.Contains("PC", StringComparison.OrdinalIgnoreCase)).ToList();

        if (pcList.Any())
            return pcList.First();
        
        return candidates.First();
    }

    private async Task<string> BuildConversationPromptAsync(Domain.AISpeechAssistant.AiSpeechAssistant assistant, CancellationToken cancellationToken)
    {
        var knowledge = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantKnowledgeAsync(assistantId: assistant.Id, isActive: true, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (knowledge == null) return null;
        
        var pstTime = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time"));
        var currentTime = pstTime.ToString("yyyy-MM-dd HH:mm:ss");
        var finalPrompt = knowledge.Prompt
            .Replace("#{current_time}", currentTime)
            .Replace("#{pst_date}", $"{pstTime.Date:yyyy-MM-dd} {pstTime.DayOfWeek}");

        if (!finalPrompt.Contains("#{customer_items}", StringComparison.OrdinalIgnoreCase)) return finalPrompt;
        
        var soldToIds = !string.IsNullOrEmpty(assistant.Name) ? assistant.Name.Split('/', StringSplitOptions.RemoveEmptyEntries).ToList() : [];
        
        if (soldToIds.Count == 0) return finalPrompt;
        
        var caches = await _salesDataProvider.GetCustomerItemsCacheBySoldToIdsAsync(soldToIds, cancellationToken).ConfigureAwait(false);
        var customerItems = caches.Where(c => !string.IsNullOrEmpty(c.CacheValue)).Select(c => c.CacheValue.Trim()).Distinct().ToList();
        finalPrompt = finalPrompt.Replace("#{customer_items}", customerItems.Any() ? string.Join(Environment.NewLine + Environment.NewLine, customerItems.Take(50)) : " ");

        Log.Information("Build conversation prompt: " + finalPrompt);
        return finalPrompt;
    }

    private async Task HandleAutoTestTaskStatusChangeAsync(AutoTestTask task, CancellationToken cancellationToken)
    {
        await _redisSafeRunner.ExecuteWithLockAsync($"auto-test-task-status-handle-{task.Id}", async () =>
        {
            var taskRecords = await _autoTestDataProvider.GetAllAutoTestTaskRecordsByTaskIdAsync(task.Id, cancellationToken).ConfigureAwait(false);

            if (taskRecords.All(x => x.Status is AutoTestTaskRecordStatus.Done or AutoTestTaskRecordStatus.Failed))
            {
                task.Status = AutoTestTaskStatus.Done;
                
                await _autoTestDataProvider.UpdateAutoTestTaskAsync(task, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            
        }, wait: TimeSpan.FromSeconds(10), retry: TimeSpan.FromSeconds(1), server: RedisServer.System).ConfigureAwait(false);
    }
    
    private List<AutoTestOrderItemDto> AutoTestOrderCompare(List<AutoTestInputDetail> realOrderItems, List<AutoTestInputDetail> aiOrderItems)
    {
        aiOrderItems ??= []; 
        realOrderItems ??= [];
        
        var orderItems = new List<AutoTestOrderItemDto>();
        
        foreach (var realOrderItem in realOrderItems)
        {
            var item = aiOrderItems.FirstOrDefault(x => x.ItemId == realOrderItem.ItemId);

            if (item == null)
            {
                orderItems.Add(new AutoTestOrderItemDto
                {
                    ItemId = realOrderItem.ItemId,
                    Quantity = realOrderItem.Quantity,
                    ItemName = realOrderItem.ItemName,
                    Status = AutoTestOrderItemStatus.Missed
                });
                
                continue;
            }
            
            orderItems.Add(new AutoTestOrderItemDto
            {
                ItemId = item.ItemId,
                Quantity = item.Quantity,
                Unit = item.Unit,
                ItemName = item.ItemName,
                Status = realOrderItem.Quantity == item.Quantity ? AutoTestOrderItemStatus.Normal : AutoTestOrderItemStatus.Abnormal
            });
        }

        foreach (var aiItem in aiOrderItems)
        {
            if (!realOrderItems.Any(x => x.ItemId == aiItem.ItemId))
            {
                orderItems.Add(new AutoTestOrderItemDto
                {
                    ItemId = aiItem.ItemId,
                    Quantity = aiItem.Quantity,
                    Unit = aiItem.Unit,
                    ItemName = aiItem.ItemName,
                    Status = AutoTestOrderItemStatus.Abnormal
                });
            }
        }
        
        return orderItems;
    }

    private async Task<string> HandleAutoTestNormalizedOutput(byte[] recording, string report, List<AutoTestInputDetail> realOrderItems,List<AutoTestOrderItemDto> aiOrderItems, CancellationToken cancellationToken)
    {
        var audio = await _attachmentService.UploadAttachmentAsync(new UploadAttachmentCommand { Attachment = new UploadAttachmentDto { FileName = Guid.NewGuid() + ".wav", FileContent = recording } }, cancellationToken).ConfigureAwait(false);
        Log.Information("Audio uploaded, url: {Url}", audio?.Attachment?.FileUrl);
        
        var normalizedOutput = new AutoTestNormalizedOutputDto
        {
            IsMatched = aiOrderItems.All(x => x.Status == AutoTestOrderItemStatus.Normal),
            Recording = audio.Attachment.FileUrl,
            AiOrder = aiOrderItems,
            ActualOrder = realOrderItems,
            Report = report
        };
        
        return JsonConvert.SerializeObject(normalizedOutput);
    }

    private async Task MarkAutoTestRecordStatusAsync(AutoTestTaskRecord record, AutoTestTaskRecordStatus status, CancellationToken cancellationToken)
    {
        record.Status = status;
        
        await _autoTestDataProvider.UpdateTaskRecordsAsync([record], true, cancellationToken).ConfigureAwait(false);
    }
    
    private async Task<T> RetryWithDelayAsync<T>(Func<CancellationToken, Task<T>> operation, Func<T, bool> shouldRetry, int maxRetryCount = 1, TimeSpan? delay = null, CancellationToken cancellationToken = default)
    {
        var result = await operation(cancellationToken).ConfigureAwait(false);
        var currentRetry = 0;
    
        while (shouldRetry(result) && currentRetry < maxRetryCount)
        {
            await Task.Delay(delay ?? TimeSpan.FromSeconds(10), cancellationToken);
            result = await operation(cancellationToken).ConfigureAwait(false);
            currentRetry++;
        }
    
        return result;
    }
    
    private async Task<AutoTestDataItem> MatchOrderAndRecordingAsync(string customerId, AutoTestCallRecordSync record, int scenarioId, int importRecordId, CancellationToken cancellationToken) 
    {
        try
        {
            var recordingUri = record.RecordingUrl;
            if (string.IsNullOrWhiteSpace(recordingUri))
            {
                Log.Warning("通话 {CallLogId} 没有录音，跳过。Customer={CustomerId}", record.CallLogId, customerId);
                return null;
            }
            
            var pacificZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
            var sapStartDate = TimeZoneInfo.ConvertTimeFromUtc(record.StartTimeUtc, pacificZone).Date;
            
            var sapResp = await RetrySapAsync(() =>
                _sapGatewayClient.QueryRecordingDataAsync(new QueryRecordingDataRequest
                {
                    CustomerId = new List<string> { customerId.PadLeft(10, '0') },
                    StartDate = sapStartDate,
                    EndDate = sapStartDate
                }, cancellationToken)).ConfigureAwait(false);

            var sapOrders = sapResp?.Data?.RecordingData ?? new List<RecordingDataItem>();
            Log.Information("SAP 返回 {Count} 条记录, Customer={CustomerId}, Date={Date}", sapOrders.Count, customerId, sapStartDate);
            if (!sapOrders.Any()) 
                return null;
            
            var oneOrderGroup = sapOrders.GroupBy(x => x.SalesDocument).SingleOrDefault();
            if (oneOrderGroup == null)
            {
                Log.Warning("SAP 未返回唯一订单, Customer={CustomerId}, Date={Date}", customerId, sapStartDate); 
                return null;
            } 
            Log.Information("匹配成功: Customer={CustomerId}, Order={OrderId}, ItemCount={Count}", customerId, oneOrderGroup.Key, oneOrderGroup.Count());
            
            var inputJsonDto = new AutoTestInputJsonDto
            {
                Recording = recordingUri,
                OrderId = oneOrderGroup.Key,
                CustomerId = customerId,
                OrderDate = sapStartDate,
                Detail = oneOrderGroup.Select((i, index) => new AutoTestInputDetail
                {
                    SerialNumber = index + 1,
                    Quantity = i.Qty,
                    ItemName = i.Description ?? string.Empty,
                    ItemId = i.Material
                }).ToList()
            };

            return new AutoTestDataItem
            {
                ScenarioId = scenarioId,
                ImportRecordId = importRecordId,
                InputJson = JsonSerializer.Serialize(inputJsonDto),
                CreatedAt = DateTimeOffset.UtcNow
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "匹配订单和录音失败 Customer={CustomerId}, CallLogId={CallLogId}", customerId, record.CallLogId); 
            return null;
        }
    }
    
    private async Task<T> RetryForeverAsync<T>(Func<Task<T>> action)
    {
        while (true)
        { 
            try
            {
                return await action().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "RingCentral 调用失败，将在60秒后继续重试（无限重试）...");
                await Task.Delay(TimeSpan.FromSeconds(60));
            }
        }
    }
    
    private async Task<T> RetrySapAsync<T>(Func<Task<T>> action, int maxRetryCount = 2, int shortDelayMs = 2000)
    {
        int currentTry = 0;
        
        while (true) 
        {
            try
            {
                return await action().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                currentTry++;
                if (currentTry > maxRetryCount) throw;
                Log.Warning(ex, "SAP 调用失败，将在 {Delay}ms 后重试…", shortDelayMs);
                await Task.Delay(shortDelayMs);
            }
        }
    }
    
    private string NormalizePhone(string phone)
    {
        if (string.IsNullOrEmpty(phone)) return phone;
        
        phone = phone.Replace("-", "").Replace(" ", "").Replace("(", "").Replace(")", "");
        
        if (!phone.StartsWith("+") && phone.Length == 10)
            phone = "+1" + phone;

        return phone;
    }
}