using System.Text.Json;
using System.Text.RegularExpressions;
using AutoMapper;
using Serilog;
using SmartTalk.Core.Ioc;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json.Linq;
using OpenAI.Chat;
using SmartTalk.Core.Constants;
using SmartTalk.Core.Domain.AISpeechAssistant;
using SmartTalk.Core.Domain.PhoneOrder;
using SmartTalk.Core.Domain.System;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Core.Services.Ffmpeg;
using SmartTalk.Core.Services.Http;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Core.Services.Jobs;
using SmartTalk.Core.Services.PhoneOrder;
using SmartTalk.Core.Settings.OpenAi;
using SmartTalk.Core.Settings.PhoneOrder;
using SmartTalk.Core.Settings.Twilio;
using SmartTalk.Messages.Dto.SpeechMatics;
using SmartTalk.Messages.Enums.PhoneOrder;
using SmartTalk.Messages.Commands.PhoneOrder;
using SmartTalk.Messages.Dto.Agent;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Dto.Sales;
using SmartTalk.Messages.Enums.Agent;
using SmartTalk.Messages.Enums.Sales;
using Twilio;
using Twilio.Rest.Api.V2010.Account;

namespace SmartTalk.Core.Services.SpeechMatics;

public interface ISpeechMaticsService : IScopedDependency
{
    Task HandleTranscriptionCallbackAsync(HandleTranscriptionCallbackCommand command, CancellationToken cancellationToken);
}

public class SpeechMaticsService : ISpeechMaticsService
{
    private readonly IMapper _mapper;
    private readonly ISalesClient _salesClient;
    private readonly IWeChatClient _weChatClient;
    private readonly IFfmpegService _ffmpegService;
    private readonly OpenAiSettings _openAiSettings;
    private readonly TwilioSettings _twilioSettings;
    private readonly ISmartiesClient _smartiesClient;
    private readonly PhoneOrderSetting _phoneOrderSetting;
    private readonly IPhoneOrderService _phoneOrderService;
    private readonly IPhoneOrderDataProvider _phoneOrderDataProvider;
    private readonly ISmartTalkHttpClientFactory _smartTalkHttpClientFactory;
    private readonly ISmartTalkBackgroundJobClient _smartTalkBackgroundJobClient;
    private readonly IAiSpeechAssistantDataProvider _aiSpeechAssistantDataProvider;
    
    public SpeechMaticsService(
        IMapper mapper,
        ISalesClient salesClient,
        IWeChatClient weChatClient,
        IFfmpegService ffmpegService,
        OpenAiSettings openAiSettings,
        TwilioSettings twilioSettings,
        ISmartiesClient smartiesClient,
        PhoneOrderSetting phoneOrderSetting,
        IPhoneOrderService phoneOrderService,
        IPhoneOrderDataProvider phoneOrderDataProvider,
        ISmartTalkHttpClientFactory smartTalkHttpClientFactory,
        ISmartTalkBackgroundJobClient smartTalkBackgroundJobClient,
        IAiSpeechAssistantDataProvider aiSpeechAssistantDataProvider)
    {
        _mapper = mapper;
        _salesClient = salesClient;
        _weChatClient = weChatClient;
        _ffmpegService = ffmpegService;
        _openAiSettings = openAiSettings;
        _twilioSettings = twilioSettings;
        _smartiesClient = smartiesClient;
        _phoneOrderSetting = phoneOrderSetting;
        _phoneOrderService = phoneOrderService;
        _phoneOrderDataProvider = phoneOrderDataProvider;
        _smartTalkHttpClientFactory = smartTalkHttpClientFactory;
        _smartTalkBackgroundJobClient = smartTalkBackgroundJobClient;
        _aiSpeechAssistantDataProvider = aiSpeechAssistantDataProvider;
    }

    public async Task HandleTranscriptionCallbackAsync(HandleTranscriptionCallbackCommand command, CancellationToken cancellationToken)
    {
        if (command.Transcription == null || command.Transcription.Results.IsNullOrEmpty() || command.Transcription.Job == null || command.Transcription.Job.Id.IsNullOrEmpty()) return;

        var record = await _phoneOrderDataProvider.GetPhoneOrderRecordByTranscriptionJobIdAsync(command.Transcription.Job.Id, cancellationToken).ConfigureAwait(false);

        Log.Information("Get Phone order record : {@record}", record);
        
        if (record == null) return;
        
        Log.Information("Transcription results : {@results}", command.Transcription.Results);
        
        try
        {
            record.Status = PhoneOrderRecordStatus.Transcription;
            
            await _phoneOrderDataProvider.UpdatePhoneOrderRecordsAsync(record, true, cancellationToken).ConfigureAwait(false);
            
            var speakInfos = StructureDiarizationResults(command.Transcription.Results);

            var audioContent = await _smartTalkHttpClientFactory.GetAsync<byte[]>(record.Url, cancellationToken).ConfigureAwait(false);
            
            await _phoneOrderService.ExtractPhoneOrderRecordAiMenuAsync(speakInfos, record, audioContent, cancellationToken).ConfigureAwait(false);
            
            await SummarizeConversationContentAsync(record, audioContent, cancellationToken).ConfigureAwait(false);
            
            await _phoneOrderDataProvider.UpdatePhoneOrderRecordsAsync(record, cancellationToken: cancellationToken).ConfigureAwait(false);
            
            _smartTalkBackgroundJobClient.Enqueue<IPhoneOrderProcessJobService>(x => x.CalculateRecordingDurationAsync(record, null, cancellationToken), HangfireConstants.InternalHostingFfmpeg);
        }
        catch (Exception e)
        {
            record.Status = PhoneOrderRecordStatus.Exception;
            
            await _phoneOrderDataProvider.UpdatePhoneOrderRecordsAsync(record, true, cancellationToken).ConfigureAwait(false);

            Log.Warning("Handle transcription callback failed: {@Exception}", e);
        }
    }
    
    private async Task SummarizeConversationContentAsync(PhoneOrderRecord record, byte[] audioContent, CancellationToken cancellationToken)
    {
        var (aiSpeechAssistant, agent) = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantByAgentIdAsync(record.AgentId, cancellationToken).ConfigureAwait(false);

        Log.Information("Get Assistant: {@Assistant} and Agent: {@Agent} by agent id {agentId}", aiSpeechAssistant, agent, record.AgentId);
        
        var callFrom = string.Empty;
        try
        {
            TwilioClient.Init(_twilioSettings.AccountSid, _twilioSettings.AuthToken);

            var call = await CallResource.FetchAsync(record.SessionId);
            callFrom = call?.From;
            
            Log.Information("Fetched incoming phone number from Twilio: {callFrom}", callFrom);
        }
        catch (Exception e)
        {
            Log.Warning("Fetched incoming phone number from Twilio failed: {Message}", e.Message);
        }

        var pstTime = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time"));
        var currentTime = pstTime.ToString("yyyy-MM-dd HH:mm:ss");
        
        var messages = await ConfigureRecordAnalyzePromptAsync(agent, aiSpeechAssistant, callFrom ?? "", currentTime, audioContent, cancellationToken);
        
        ChatClient client = new("gpt-4o-audio-preview", _openAiSettings.ApiKey);
        ChatCompletionOptions options = new() { ResponseModalities = ChatResponseModalities.Text };

        ChatCompletion completion = await client.CompleteChatAsync(messages, options, cancellationToken);
        Log.Information("sales record analyze report:" + completion.Content.FirstOrDefault()?.Text);
        
        record.Status = PhoneOrderRecordStatus.Sent;
        record.TranscriptionText = completion.Content.FirstOrDefault()?.Text ?? "";

        await MultiScenarioCustomProcessingAsync(agent, aiSpeechAssistant, record, cancellationToken).ConfigureAwait(false);
        
        if (agent.SourceSystem == AgentSourceSystem.Smarties)
            await CallBackSmartiesRecordAsync(agent, record, cancellationToken).ConfigureAwait(false);

        var message = agent.WechatRobotMessage?.Replace("#{assistant_name}", aiSpeechAssistant?.Name ?? "").Replace("#{agent_id}", agent.Id.ToString()).Replace("#{record_id}", record.Id.ToString()).Replace("#{assistant_file_url}", record.Url);

        message = await SwitchKeyMessageByGetUserProfileAsync(record, callFrom, aiSpeechAssistant, agent, message, cancellationToken).ConfigureAwait(false);

        await SendWorkWechatMessageByRobotKeyAsync(message, record, audioContent, agent, aiSpeechAssistant, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> SwitchKeyMessageByGetUserProfileAsync(PhoneOrderRecord record, string callFrom, Domain.AISpeechAssistant.AiSpeechAssistant aiSpeechAssistant, Agent agent, string message, CancellationToken cancellationToken)
    {
        if (callFrom != null && aiSpeechAssistant?.Id != null && !string.IsNullOrEmpty(message))
        {
            var userProfile = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantUserProfileAsync(aiSpeechAssistant.Id, callFrom, cancellationToken).ConfigureAwait(false);
            var salesName = userProfile?.ProfileJson != null ? JObject.Parse(userProfile.ProfileJson).GetValue("correspond_sales")?.ToString() : string.Empty;
            
            var salesDisplayName = !string.IsNullOrEmpty(salesName) ? $"{salesName}" : "";

            message = message.Replace("#{sales_name}", salesDisplayName);
        }

        return message;
    }

    private async Task SendWorkWechatMessageByRobotKeyAsync(string message, PhoneOrderRecord record, byte[] audioContent, Agent agent, Domain.AISpeechAssistant.AiSpeechAssistant aiSpeechAssistant, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(agent.WechatRobotKey) && !string.IsNullOrEmpty(message))
        {
            if (agent.IsWecomMessageOrder && aiSpeechAssistant != null)
            {
                var messageNumber = await SendAgentMessageRecordAsync(agent, record.Id, aiSpeechAssistant.GroupKey, cancellationToken).ConfigureAwait(false);
                message = $"【第{messageNumber}條】\n" + message;
            }

            if (agent.IsSendAnalysisReportToWechat && !string.IsNullOrEmpty(record.TranscriptionText))
            {
                message += "\n\n" + record.TranscriptionText;
            }

            await _phoneOrderService.SendWorkWeChatRobotNotifyAsync(audioContent, agent.WechatRobotKey, message, Array.Empty<string>(), cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task CallBackSmartiesRecordAsync(Agent agent, PhoneOrderRecord record, CancellationToken cancellationToken = default)
    {
        if (agent.Type == AgentType.AiKid)
        {
            var aiKid = await _aiSpeechAssistantDataProvider.GetAiKidAsync(agentId: agent.Id, cancellationToken: cancellationToken).ConfigureAwait(false);
        
            Log.Information("Get ai kid: {@Kid} by agentId: {AgentId}", aiKid, agent.Id);

            if (aiKid == null)throw new Exception($"Could not found ai kid by agentId: {agent.Id}");
        
            await _smartiesClient.CallBackSmartiesAiKidRecordAsync(new AiKidCallBackRequestDto
            {
                Url = record.Url,
                Uuid = aiKid.KidUuid,
                SessionId = record.SessionId
            }, cancellationToken).ConfigureAwait(false);
        }
        else
            await _smartiesClient.CallBackSmartiesAiSpeechAssistantRecordAsync(new AiSpeechAssistantCallBackRequestDto { CallSid = record.SessionId, RecordUrl = record.Url, RecordAnalyzeReport =  record.TranscriptionText }, cancellationToken).ConfigureAwait(false);
    }

    private async Task<int> SendAgentMessageRecordAsync(Agent agent, int recordId, int groupKey, CancellationToken cancellationToken)
    {
        var timezone = !string.IsNullOrWhiteSpace(agent.Timezone) ? TimeZoneInfo.FindSystemTimeZoneById(agent.Timezone) : TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
        var nowDate = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, timezone);

        var utcDate = TimeZoneInfo.ConvertTimeToUtc(nowDate.Date, timezone);

        var existingCount = await _aiSpeechAssistantDataProvider.GetMessageCountByAgentAndDateAsync(groupKey, utcDate, cancellationToken).ConfigureAwait(false);

        var messageNumber = existingCount + 1;

        var newRecord = new AgentMessageRecord
        {
            AgentId = agent.Id,
            GroupKey = groupKey,
            RecordId = recordId,
            MessageNumber = messageNumber
        };

        await _aiSpeechAssistantDataProvider.AddAgentMessageRecordAsync(newRecord, cancellationToken).ConfigureAwait(false);

        return messageNumber;
    }
    
    private List<SpeechMaticsSpeakInfoDto> StructureDiarizationResults(List<SpeechMaticsResultDto> results)
    {
        string currentSpeaker = null;
        PhoneOrderRole? currentRole = null;
        var startTime = 0.0;
        var endTime = 0.0;
        var speakInfos = new List<SpeechMaticsSpeakInfoDto>();

        foreach (var result in results.Where(result => !result.Alternatives.IsNullOrEmpty()))
        {
            if (currentSpeaker == null)
            {
                currentSpeaker = result.Alternatives[0].Speaker;
                currentRole = PhoneOrderRole.Restaurant;
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
                speakInfos.Add(new SpeechMaticsSpeakInfoDto { EndTime = endTime, StartTime = startTime, Speaker = currentSpeaker, Role = currentRole.Value });
                currentSpeaker = result.Alternatives[0].Speaker;
                currentRole = currentRole == PhoneOrderRole.Restaurant ? PhoneOrderRole.Client : PhoneOrderRole.Restaurant;
                startTime = result.StartTime;
                endTime = result.EndTime;
            }
        }

        speakInfos.Add(new SpeechMaticsSpeakInfoDto { EndTime = endTime, StartTime = startTime, Speaker = currentSpeaker });

        Log.Information("Structure diarization results : {@speakInfos}", speakInfos);
        
        return speakInfos;
    }
    
    private async Task<List<ChatMessage>> ConfigureRecordAnalyzePromptAsync(Agent agent, Domain.AISpeechAssistant.AiSpeechAssistant aiSpeechAssistant, string callFrom, string currentTime, byte[] audioContent, CancellationToken cancellationToken) 
    {
        var askItemsJson = string.Empty;

        if (agent.Type == AgentType.Sales)
        {
            var sales = await _aiSpeechAssistantDataProvider.GetCallInSalesByNameAsync(aiSpeechAssistant.Name, SalesCallType.CallIn, cancellationToken).ConfigureAwait(false);
            Log.Information("Sales fetch result: {@Sales}", sales);

            if (sales != null)
            {
                var requestDto = new GetAskInfoDetailListByCustomerRequestDto
                {
                    CustomerNumbers = new List<string> { aiSpeechAssistant.Name }
                };

                var askedItems = await _salesClient.GetAskInfoDetailListByCustomerAsync(requestDto, cancellationToken).ConfigureAwait(false);
                
                if (askedItems?.Data == null || !askedItems.Data.Any())
                {
                    Log.Warning("Sales API 返回空数据，客户：{Customer}", aiSpeechAssistant.Name);
                    
                    askedItems = new GetAskInfoDetailListByCustomerResponseDto { Data = new List<VwAskDetail>() };
                }

                var topItems = askedItems.Data.OrderByDescending(x => x.ValidAskQty).Take(60).ToList();

                var simplifiedItems = topItems.Select(x => new
                {
                    name = x.MaterialDesc,
                    quantity = x.ValidAskQty,
                    materialNumber = x.Material
                }).ToList();
                
                askItemsJson = JsonSerializer.Serialize(simplifiedItems, new JsonSerializerOptions { WriteIndented = true });
                Log.Information("Serialized AskItems JSON: {AskItemsJson}", askItemsJson);
            }
        }

        var audioData = BinaryData.FromBytes(audioContent);
            List<ChatMessage> messages =
            [
                new SystemChatMessage(string.IsNullOrEmpty(aiSpeechAssistant?.CustomRecordAnalyzePrompt)
                    ? "你是一名電話錄音的分析員，通過聽取錄音內容和語氣情緒作出精確分析，冩出一份分析報告。\n\n分析報告的格式：交談主題：xxx\n\n 來電號碼：#{call_from}\n\n 內容摘要:xxx \n\n 客人情感與情緒: xxx \n\n 待辦事件: \n1.xxx\n2.xxx \n\n 客人下單內容(如果沒有則忽略)：1. 牛肉(1箱)\n2.雞腿肉(1箱)".Replace("#{call_from}", callFrom ?? "")
                    : aiSpeechAssistant.CustomRecordAnalyzePrompt.Replace("#{call_from}", callFrom ?? "").Replace("#{current_time}", currentTime).Replace("#{askItemsJson}", askItemsJson ?? "")),
                new UserChatMessage(ChatMessageContentPart.CreateInputAudioPart(audioData, ChatInputAudioFormat.Wav)),
                new UserChatMessage("幫我根據錄音生成分析報告：")
            ];

            return messages;
        }

    private async Task MultiScenarioCustomProcessingAsync(Agent agent, Domain.AISpeechAssistant.AiSpeechAssistant aiSpeechAssistant, PhoneOrderRecord record, CancellationToken cancellationToken) 
    { 
        switch (agent.Type) 
        { 
            case AgentType.Sales: 
                if (!string.IsNullOrEmpty(record.TranscriptionText)) 
                { 
                    await HandleSalesScenarioAsync(agent, aiSpeechAssistant, record, cancellationToken).ConfigureAwait(false);
                }
                break; 
        } 
    }
    
    private async Task HandleSalesScenarioAsync(Agent agent, Domain.AISpeechAssistant.AiSpeechAssistant aiSpeechAssistant, PhoneOrderRecord record, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(record.TranscriptionText)) return;

        var askInfoResponse = await _salesClient.GetAskInfoDetailListByCustomerAsync(new GetAskInfoDetailListByCustomerRequestDto { CustomerNumbers = new List<string> { aiSpeechAssistant.Name } }, cancellationToken).ConfigureAwait(false);

        var historyItems = askInfoResponse?.Data?.Where(x => !string.IsNullOrWhiteSpace(x.Material)).Select(x => (x.Material, x.MaterialDesc)).ToList() ?? new List<(string Material, string MaterialDesc)>();

        var (extractedOrderItems, deliveryDate, storeNumber) = await ExtractAndMatchOrderItemsFromReportAsync(record.TranscriptionText, historyItems, DateTime.Today, cancellationToken).ConfigureAwait(false);

        if (!extractedOrderItems.Any()) return;
        
        var (soldToId, soldToIds) = GetSoldToFields(aiSpeechAssistant.Name, storeNumber);

        var draftOrder = new GenerateAiOrdersRequestDto
        {
            AiOrderInfoDto = new AiOrderInfoDto
            {
                SoldToId = soldToId,
                SoldToIds = soldToIds,
                DocumentDate = DateTime.Today,
                DeliveryDate = deliveryDate,
                AiOrderItemDtoList = extractedOrderItems
            }
        };

        await _salesClient.GenerateAiOrdersAsync(draftOrder, cancellationToken).ConfigureAwait(false);
        Log.Information("GenerateAiOrdersAsync call completed successfully.");
    }

    private async Task<(List<AiOrderItemDto> Items, DateTime DeliveryDate, string storeNumber)> ExtractAndMatchOrderItemsFromReportAsync(string reportText, List<(string Material, string MaterialDesc)> historyItems, DateTime orderDate, CancellationToken cancellationToken) 
    { 
        var client = new ChatClient("gpt-4.1", _openAiSettings.ApiKey);
        
        var materialListText = string.Join("\n", historyItems.Select(x => $"{x.MaterialDesc} ({x.Material})"));

        var systemPrompt = "你是一名订单分析助手。请从下面的客户分析报告文本中提取：\n" +
                           "1. 所有下单的物料名称和数量，并用历史物料列表匹配每个物料的materialNumber；\n" +
                           "2. 预约送货时间（格式yyyy-MM-dd，如果没有则为空）；\n" +
                           "3. 客户下单的storeNumber（如number one、number two，如果没有则为空）；\n" +
                           "返回JSON，格式如下，每个元素包含：\n" +
                           "- name: 物料名称\n" +
                           "- quantity: 数量\n" +
                           "- materialNumber: 物料编码\n" +
                           "- deliveryDate: 客户预约送货日期\n" +
                           "- storeNumber: 客户下单storeNumber（如果没有则空字符串）\n\n" +
                           materialListText + "\n\n客户分析报告文本：\n" + reportText +
                           "\n请只返回JSON数组，不要多余说明。";
        Log.Information("Sending prompt to GPT: {Prompt}", systemPrompt);

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt),
        };
        
        var completion = await client.CompleteChatAsync(messages, new ChatCompletionOptions { ResponseModalities = ChatResponseModalities.Text, ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat() }, cancellationToken).ConfigureAwait(false);
        var jsonResponse = completion.Value.Content.FirstOrDefault()?.Text ?? "";
        Log.Information("AI JSON Response: {JsonResponse}", jsonResponse);
        
        try 
        { 
            using var jsonDoc = JsonDocument.Parse(jsonResponse);
            
            var resultArray = jsonDoc.RootElement.TryGetProperty("result", out var resultProp) ? resultProp : jsonDoc.RootElement;
            var parsedItems = JsonSerializer.Deserialize<List<ExtractedOrderItemDto>>(resultArray.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<ExtractedOrderItemDto>();
            
            var deliveryDateStr = parsedItems.Select(i => i.DeliveryDate).FirstOrDefault(d => !string.IsNullOrEmpty(d));
            DateTime deliveryDate;
            if (!DateTime.TryParse(deliveryDateStr, out deliveryDate))
            {
                deliveryDate = DateTime.Today;
            }
            
            var storeNumber = parsedItems.Select(i => i.StoreNumber).FirstOrDefault(s => !string.IsNullOrEmpty(s));

            var aiOrderItems = _mapper.Map<List<AiOrderItemDto>>(parsedItems);

            return (aiOrderItems, deliveryDate, storeNumber);
        }
        catch (Exception ex) 
        { 
            Log.Warning("解析GPT返回JSON失败: {Message}", ex.Message); 
            return (new List<AiOrderItemDto>(), DateTime.Today, null);
        } 
    }

    private (string SoldToId, string SoldToIds) GetSoldToFields(string customerName, string storeNumber)
    {
        var customerIds = customerName.Split('/');

        if (customerIds.Length == 1) return (customerIds[0], null);

        if (string.IsNullOrEmpty(storeNumber)) return (null, null);
        
        var mappedId = MapStoreNumberToId(storeNumber, customerIds); 
        return (null, mappedId);
    }

    private string MapStoreNumberToId(string storeNumber, string[] allIds)
    {
        var match = Regex.Match(storeNumber.ToLower(), @"\b(one|two|three|four|five)\b");
        if (match.Success)
        {
            var index = match.Groups[1].Value switch
            {
                "one" => 0,
                "two" => 1,
                "three" => 2,
                "four" => 3,
                "five" => 4,
                _ => -1
            };
            if (index >= 0 && index < allIds.Length) return allIds[index];
        }
        return null;
    }
}