using Serilog;
using System.Text;
using OpenAI.Chat;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using AutoMapper;
using SmartTalk.Core.Ioc;
using Google.Cloud.Translation.V2;
using SmartTalk.Core.Services.STT;
using SmartTalk.Core.Services.Http;
using Smarties.Messages.DTO.OpenAi;
using Microsoft.IdentityModel.Tokens;
using Smarties.Messages.Enums.OpenAi;
using SmartTalk.Core.Settings.OpenAi;
using Smarties.Messages.Requests.Ask;
using SmartTalk.Core.Domain.AutoTest;
using SmartTalk.Core.Domain.SpeechMatics;
using SmartTalk.Core.Domain.System;
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
using SmartTalk.Core.Services.Sale;
using SmartTalk.Messages.Commands.Attachments;
using SmartTalk.Messages.Dto.Attachments;
using SmartTalk.Messages.Dto.Sales;
using TranscriptionFileType = SmartTalk.Messages.Enums.STT.TranscriptionFileType;
using TranscriptionResponseFormat = SmartTalk.Messages.Enums.STT.TranscriptionResponseFormat;

namespace SmartTalk.Core.Services.AutoTest.SalesAiOrder;

public interface IAutoTestSalesPhoneOrderProcessJobService : IScopedDependency
{
    Task StartTestingSalesPhoneOrderTaskAsync(int taskId, int taskRecordId, CancellationToken cancellationToken);
    
    Task HandleTestingSalesPhoneOrderSpeechMaticsCallBackAsync(string jobId, CancellationToken cancellationToken);
}

public class AutoTestSalesPhoneOrderProcessJobService : IAutoTestSalesPhoneOrderProcessJobService
{
    private readonly IMapper _mapper;
    private readonly ISalesClient _salesClient;
    private readonly IFfmpegService _ffmpegService;
    private readonly OpenAiSettings _openAiSettings;
    private readonly ISmartiesClient _smartiesClient;
    private readonly IRedisSafeRunner _redisSafeRunner;
    private readonly TranslationClient _translationClient;
    private readonly IAttachmentService _attachmentService;
    private readonly ISalesDataProvider _salesDataProvider;
    private readonly ISpeechToTextService _speechToTextService;
    private readonly ISpeechMaticsService _speechMaticsService;
    private readonly IAutoTestDataProvider _autoTestDataProvider;
    private readonly ISmartTalkHttpClientFactory _httpClientFactory;
    private readonly ISpeechMaticsDataProvider _speechMaticsDataProvider;
    private readonly ISmartTalkHttpClientFactory _smartTalkHttpClientFactory;
    private readonly IAiSpeechAssistantDataProvider _aiSpeechAssistantDataProvider;

    public AutoTestSalesPhoneOrderProcessJobService(
        IMapper mapper,
        ISalesClient salesClient,
        IFfmpegService ffmpegService,
        OpenAiSettings openAiSettings,
        ISmartiesClient smartiesClient,
        IRedisSafeRunner redisSafeRunner,
        TranslationClient translationClient,
        IAttachmentService attachmentService,
        ISalesDataProvider salesDataProvider,
        ISpeechMaticsService speechMaticsService,
        ISpeechToTextService speechToTextService,
        IAutoTestDataProvider autoTestDataProvider,
        ISmartTalkHttpClientFactory httpClientFactory,
        ISpeechMaticsDataProvider speechMaticsDataProvider,
        ISmartTalkHttpClientFactory smartTalkHttpClientFactory,
        IAiSpeechAssistantDataProvider aiSpeechAssistantDataProvider)
    {
        _mapper = mapper;
        _salesClient = salesClient;
        _ffmpegService = ffmpegService;
        _openAiSettings = openAiSettings;
        _smartiesClient = smartiesClient;
        _redisSafeRunner = redisSafeRunner;
        _attachmentService = attachmentService;
        _salesDataProvider = salesDataProvider;
        _translationClient = translationClient;
        _httpClientFactory = httpClientFactory;
        _speechToTextService = speechToTextService;
        _speechMaticsService = speechMaticsService;
        _autoTestDataProvider = autoTestDataProvider;
        _speechMaticsDataProvider = speechMaticsDataProvider;
        _smartTalkHttpClientFactory = smartTalkHttpClientFactory;
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

    private async Task ProcessingTestSalesPhoneOrderSpeechMaticsCallBackAsync(
        AutoTestTaskRecord record, Domain.AISpeechAssistant.AiSpeechAssistant assistant, SpeechMaticsJob speechMaticsJob, CancellationToken cancellationToken)
    {
        try
        {
            var audioBytes = await FetchingRecordAudioAsync(record, cancellationToken).ConfigureAwait(false);
            if (audioBytes == null) record.Status = AutoTestTaskRecordStatus.Failed;

            var customerAudios = await ExtractingCustomerAudioAsync(speechMaticsJob, audioBytes, cancellationToken).ConfigureAwait(false);
            if (customerAudios == null || customerAudios.Count == 0) record.Status = AutoTestTaskRecordStatus.Failed;
 
            var conversationAudios = await ProcessAudioConversationAsync(customerAudios, assistant, cancellationToken).ConfigureAwait(false);
            if (conversationAudios == null || conversationAudios.Length == 0) record.Status = AutoTestTaskRecordStatus.Failed;
        
            var (report, aiOrder) = await GenerateSalesAiOrderAsync(assistant, conversationAudios, cancellationToken).ConfigureAwait(false);

            var inputSnapshot = JsonConvert.DeserializeObject<AutoTestInputJsonDto>(record.InputSnapshot);
            var comparedAiOrderItems = AutoTestOrderCompare(inputSnapshot.Detail, aiOrder);
            var normalizedOutput = await HandleAutoTestNormalizedOutput(conversationAudios, report, inputSnapshot.Detail, comparedAiOrderItems, cancellationToken).ConfigureAwait(false);

            record.Status = AutoTestTaskRecordStatus.Done;
            record.NormalizedOutput = normalizedOutput;
        }
        catch (Exception e)
        {
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

            if (root.TryGetProperty("recording", out var recordingElement))
            {
                recording = recordingElement.GetString();
            }
        }
        
        if (recording == null) return null;
        
        var audioContent = await _smartTalkHttpClientFactory.GetAsync<byte[]>(recording, cancellationToken).ConfigureAwait(false);
        
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
        var segments = query.Split(new[] { ";", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        var speakerVotes = new Dictionary<string, int> { { "S1", 0 }, { "S2", 0 } };

        foreach (var segment in segments.Take(12))
        {
            var completionResult = await _smartiesClient.PerformQueryAsync(new AskGptRequest
            {
                Messages = new List<CompletionsRequestMessageDto>
                {
                    new()
                    {
                        Role = "system",
                        Content = new CompletionsStringContent(
                            "你是一款销售与餐厅老板对话高度理解的智能助手。" +
                            "请根据我提供的对话判断餐厅老板是哪个 speaker。" +
                            "输出规则:" +
                            "1. 如果 S1 是餐厅老板，请返回 S1；S2 是餐厅老板返回 S2。" +
                            "2. 如果无法确定，请结合语义规则判断：" +
                            "   - 说下单/要求发货/订货/采购的一般是餐厅老板（客户）；" +
                            "   - 说问候/确认/重复信息/模板话术的一般是客服；" +
                            "   - 熟悉流程的客户会直接报要订哪些货，一直回复好的是客服；" +
                            "3. 若仍无法判断，默认返回 S1。" +
                            "样例:\n" +
                            "S1: 我要订货; S2: 好的; output:S1\n" +
                            "S1: 老板您好，我们有新货; S2: 给我留三斤; output:S2\n" +
                            "S1: 最近土豆质量好吗; S2: 很好; S1: 我要两箱; output:S1\n" +
                            "S1: 一箱牛肉；S2: 好；output:S1\n" +
                            "S1: 最近青豆的价格如何；S2: 和之前一样，5美金一磅；S1:那要50磅青豆\n"+
                            "S!: 上次的猪肉用得合适吗，最近都有货；S2: 还没用完，暂时不需要； S1: 好的，需要可以再联系我")
                    },
                    new()
                    {
                        Role = "user",
                        Content = new CompletionsStringContent($"input: {segment}, output:")
                    }
                },
                Model = OpenAiModel.Gpt4o
            }, cancellationToken).ConfigureAwait(false);

            var result = completionResult.Data.Response?.Trim();
            if (result == "S1" || result == "S2") speakerVotes[result]++;
        }

        var finalSpeaker = speakerVotes.MaxBy(kv => kv.Value).Key;

        if (string.IsNullOrEmpty(finalSpeaker)) finalSpeaker = "S1";

        Log.Information("CheckAudioSpeakerIsCustomerAsync finalSpeaker: {Speaker}", finalSpeaker);
        return finalSpeaker;
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

                var completion = await client.CompleteChatAsync(conversationHistory, options, cancellationToken);

                var aiWavFile = Path.GetTempFileName() + ".wav";
                await File.WriteAllBytesAsync(aiWavFile, completion.Value.OutputAudio.AudioBytes.ToArray(), cancellationToken);

                wavFiles.Add(aiWavFile);

                conversationHistory.Add(new AssistantChatMessage(completion.Value.OutputAudio.Transcript));

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
        var customerItemsString = string.Join(Environment.NewLine, soldToIds.Select(id => customerItemsCacheList.FirstOrDefault(c => c.CacheKey == id)?.CacheValue ?? ""));

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
    
    private string MatchMaterialNumber(string itemName, string baseNumber, string unit,
        List<(string Material, string MaterialDesc, DateTime? invoiceDate)> historyItems)
    {
        var candidates = historyItems
            .Where(x => x.MaterialDesc != null && x.MaterialDesc.Contains(itemName, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Material).ToList();
        Log.Information("Candidate material code list: {@Candidates}", candidates);

        if (!candidates.Any()) return string.IsNullOrEmpty(baseNumber) ? "" : baseNumber;
        
        if (candidates.Count == 1) return candidates.First();

        if (!string.IsNullOrWhiteSpace(unit))
        {
            var u = unit.ToLower();
            if (u.Contains("case") || u.Contains("箱"))
            {
                var csItem = candidates.FirstOrDefault(x => x.EndsWith("CS", StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(csItem)) return csItem;
            }
            else
            {
                var pcItem = candidates.FirstOrDefault(x => x.EndsWith("PC", StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(pcItem)) return pcItem;
            }
        }

        var pureNumber = candidates.FirstOrDefault(x => Regex.IsMatch(x, @"^\d+$"));
        return pureNumber ?? candidates.First();
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
}