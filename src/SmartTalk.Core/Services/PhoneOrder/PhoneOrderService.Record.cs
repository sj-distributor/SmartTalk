using System.Reflection;
using Serilog;
using System.Text;
using System.Text.Json.Serialization;
using SmartTalk.Messages.Enums.STT;
using Smarties.Messages.DTO.OpenAi;
using SmartTalk.Messages.Dto.Agent;
using SmartTalk.Messages.Dto.EasyPos;
using Microsoft.IdentityModel.Tokens;
using Smarties.Messages.Enums.OpenAi;
using Smarties.Messages.Requests.Ask;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SmartTalk.Core.Domain.PhoneOrder;
using SmartTalk.Core.Domain.Pos;
using SmartTalk.Core.Domain.SpeechMatics;
using SmartTalk.Core.Services.Linphone;
using SmartTalk.Messages.Dto.PhoneOrder;
using SmartTalk.Messages.Dto.Attachments;
using SmartTalk.Messages.Enums.PhoneOrder;
using SmartTalk.Messages.Dto.SpeechMatics;
using SmartTalk.Messages.Enums.SpeechMatics;
using SmartTalk.Messages.Commands.PhoneOrder;
using SmartTalk.Messages.Requests.PhoneOrder;
using SmartTalk.Messages.Commands.Attachments;
using SmartTalk.Messages.Dto.WeChat;
using SmartTalk.Messages.Enums.Pos;
using SmartTalk.Messages.Events.PhoneOrder;
using TranscriptionFileType = SmartTalk.Messages.Enums.STT.TranscriptionFileType;
using TranscriptionResponseFormat = SmartTalk.Messages.Enums.STT.TranscriptionResponseFormat;

namespace SmartTalk.Core.Services.PhoneOrder;

public partial interface IPhoneOrderService
{
    Task<GetPhoneOrderRecordsResponse> GetPhoneOrderRecordsAsync(GetPhoneOrderRecordsRequest request, CancellationToken cancellationToken);
    
    Task<PhoneOrderRecordUpdatedEvent> UpdatePhoneOrderRecordAsync(UpdatePhoneOrderRecordCommand command, CancellationToken cancellationToken);

    Task ReceivePhoneOrderRecordAsync(ReceivePhoneOrderRecordCommand command, CancellationToken cancellationToken);

    Task ExtractPhoneOrderRecordAiMenuAsync(List<SpeechMaticsSpeakInfoDto> phoneOrderInfo, PhoneOrderRecord record, byte[] audioContent, CancellationToken cancellationToken);

    Task<AddOrUpdateManualOrderResponse> AddOrUpdateManualOrderAsync(AddOrUpdateManualOrderCommand command, CancellationToken cancellationToken);
    
    Task<GetPhoneCallUsagesPreviewResponse> GetPhoneCallUsagesPreviewAsync(GetPhoneCallUsagesPreviewRequest request, CancellationToken cancellationToken);

    Task<GetPhoneCallRecordDetailResponse> GetPhoneCallrecordDetailAsync(GetPhoneCallRecordDetailRequest request, CancellationToken cancellationToken);

    Task<GetPhoneOrderCompanyCallReportResponse> GetPhoneOrderCompanyCallReportAsync(GetPhoneOrderCompanyCallReportRequest request, CancellationToken cancellationToken);

    Task<GetPhoneOrderRecordReportResponse> GetPhoneOrderRecordReportByCallSidAsync(GetPhoneOrderRecordReportRequest request, CancellationToken cancellationToken);
    
    Task<GetPhoneOrderDataDashboardResponse> GetPhoneOrderDataDashboardAsync(GetPhoneOrderDataDashboardRequest request, CancellationToken cancellationToken);
    
    Task<GetPhoneOrderRecordScenarioResponse> GetPhoneOrderRecordScenarioAsync(GetPhoneOrderRecordScenarioRequest request, CancellationToken cancellationToken);
    
    Task<GetPhoneOrderRecordTasksResponse> GetPhoneOrderRecordTasksRequestsAsync(GetPhoneOrderRecordTasksRequest request, CancellationToken cancellationToken);

    Task<UpdatePhoneOrderRecordTasksResponse> UpdatePhoneOrderRecordTasksAsync(UpdatePhoneOrderRecordTasksCommand command, CancellationToken cancellationToken);
}

public partial class PhoneOrderService
{
    public async Task<GetPhoneOrderRecordsResponse> GetPhoneOrderRecordsAsync(GetPhoneOrderRecordsRequest request, CancellationToken cancellationToken)
    {
        var (utcStart, utcEnd) = ConvertPstDateToUtcRange(request.Date);

        var agentIds = request.AgentId.HasValue
            ? [request.AgentId.Value]
            : request.StoreId.HasValue
                ? (await _posDataProvider.GetPosAgentsAsync(storeIds: [request.StoreId.Value], cancellationToken: cancellationToken).ConfigureAwait(false)).Select(x => x.AgentId).ToList()
                : [];
        
        Log.Information("Get phone order records: {@AgentIds}, {Name}, {Start}, {End}, {OrderIds}, {DialogueScenarios}, {AssistantId}", agentIds, request.Name, utcStart, utcEnd, request.OrderIds, request.DialogueScenarios, request.AssistantId);
        
        var records = await _phoneOrderDataProvider.GetPhoneOrderRecordsAsync(agentIds, request.Name, utcStart, utcEnd, request.DialogueScenarios, request.AssistantId,request.OrderIds, cancellationToken).ConfigureAwait(false);
        
        Log.Information("Get phone order records Count: {@Count}", records.Count);
        
        var enrichedRecords = _mapper.Map<List<PhoneOrderRecordDto>>(records);
        
        await BuildRecordUnreviewDataAsync(enrichedRecords, cancellationToken).ConfigureAwait(false);
        
        return new GetPhoneOrderRecordsResponse
        {
            Data = enrichedRecords
        };
    }

    public async Task<PhoneOrderRecordUpdatedEvent> UpdatePhoneOrderRecordAsync(UpdatePhoneOrderRecordCommand command, CancellationToken cancellationToken)
    {
        var records = await _phoneOrderDataProvider.GetPhoneOrderRecordAsync(command.RecordId, cancellationToken: cancellationToken).ConfigureAwait(false);

        var record = records.FirstOrDefault();
        if (record == null) throw new Exception($"Phone order record not found: {command.RecordId}");

        if (record.IsLockedScenario) throw new Exception("The record scenario was locked.");
        
        var user = await _accountDataProvider.GetUserAccountByUserIdAsync(command.UserId, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (user == null) 
            throw new Exception($"User not found: {command.UserId}");
        
        var originalScenario = record.Scenario;
        
        record.Scenario = command.DialogueScenarios;
        record.IsModifyScenario = true;
        await _phoneOrderDataProvider.UpdatePhoneOrderRecordsAsync(record,  true, cancellationToken);
        
        await _phoneOrderDataProvider.AddPhoneOrderRecordScenarioHistoryAsync(new PhoneOrderRecordScenarioHistory
        {
            RecordId = record.Id,
            Scenario = record.Scenario.GetValueOrDefault(),
            UpdatedBy = user.Id,
            ModifyType = ModifyType.CallType,
            UserName = user.UserName,
            CreatedDate = DateTime.UtcNow
        }, true, cancellationToken).ConfigureAwait(false);

        return new PhoneOrderRecordUpdatedEvent
        {
            RecordId = record.Id,
            UserName = user.UserName,
            OriginalScenarios = originalScenario,
            DialogueScenarios = record.Scenario.GetValueOrDefault()
        };
    }

    public async Task ReceivePhoneOrderRecordAsync(ReceivePhoneOrderRecordCommand command, CancellationToken cancellationToken)
    {
        if (command.RecordName.IsNullOrEmpty() && command.RecordUrl.IsNullOrEmpty()) return;
        
        if (!string.IsNullOrEmpty(command.RecordUrl))
            command.RecordContent = await _httpClientFactory.GetAsync<byte[]>(command.RecordUrl, cancellationToken).ConfigureAwait(false);

        var recordInfo = await ExtractPhoneOrderRecordInfoAsync(command.RecordName, command.AgentId, command.CreatedDate, cancellationToken).ConfigureAwait(false);

        Log.Information("Phone order record information: {@recordInfo}", recordInfo);

        if (recordInfo == null) return;
        if (await CheckOrderExistAsync(command.AgentId, recordInfo.StartDate, cancellationToken).ConfigureAwait(false)) return;

        var transcription = await _speechToTextService.SpeechToTextAsync(
                command.RecordContent, fileType: TranscriptionFileType.Wav, responseFormat: TranscriptionResponseFormat.Text, cancellationToken: cancellationToken).ConfigureAwait(false);

        var detection = await _translationClient.DetectLanguageAsync(transcription, cancellationToken).ConfigureAwait(false);

        Log.Information("Phone order record transcription detected language: {@detectionLanguage}", detection.Language);
        
        var record = new PhoneOrderRecord
        {
            SessionId = Guid.NewGuid().ToString(), AgentId = recordInfo.Agent.Id,
            Language = SelectLanguageEnum(detection.Language), CreatedDate = recordInfo.StartDate,
            Status = PhoneOrderRecordStatus.Recieved,
            OrderRecordType = command.OrderRecordType,
            PhoneNumber = recordInfo.PhoneNumber 
        };

        if (await CheckPhoneOrderRecordDurationAsync(command.RecordContent, cancellationToken).ConfigureAwait(false))
        {
            await AddPhoneOrderRecordAsync(record, PhoneOrderRecordStatus.NoContent, cancellationToken).ConfigureAwait(false);

            return;
        }

        record.Url = command.RecordUrl ?? await UploadRecordFileAsync(command.RecordName, command.RecordContent, cancellationToken).ConfigureAwait(false);

        Log.Information($"Phone order record file url: {record.Url}", record.Url);

        if (string.IsNullOrEmpty(record.Url))
        {
            await AddPhoneOrderRecordAsync(record, PhoneOrderRecordStatus.NoContent, cancellationToken).ConfigureAwait(false);

            return;
        }

        record.TranscriptionJobId = await _speechMaticsService.CreateSpeechMaticsJobAsync(command.RecordContent, command.RecordName ?? Guid.NewGuid().ToString("N") + ".wav", detection.Language, SpeechMaticsJobScenario.Released, cancellationToken).ConfigureAwait(false);

        await AddPhoneOrderRecordAsync(record, PhoneOrderRecordStatus.Diarization, cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> CheckOrderExistAsync(int agentId, DateTimeOffset createdDate, CancellationToken cancellationToken)
    {
        return (await _phoneOrderDataProvider.GetPhoneOrderRecordsAsync(agentId: agentId, createdDate: createdDate, cancellationToken: cancellationToken).ConfigureAwait(false)).Any();
    }

    public TranscriptionLanguage SelectLanguageEnum(string language)
    {
        return language switch
        {
            "zh" or "zh-CN" or "zh-TW" => TranscriptionLanguage.Chinese,
            "en" => TranscriptionLanguage.English,
            _ => TranscriptionLanguage.English
        };
    }

    public async Task ExtractPhoneOrderRecordAiMenuAsync(
        List<SpeechMaticsSpeakInfoDto> phoneOrderInfo, PhoneOrderRecord record, byte[] audioContent, CancellationToken cancellationToken)
    {
        if (phoneOrderInfo is { Count: 0 }) return;

        try
        {
            phoneOrderInfo = await HandlerConversationFirstSentenceAsync(phoneOrderInfo, record, audioContent, cancellationToken).ConfigureAwait(false);

            Log.Information("Phone order record info: {@phoneOrderInfo}", phoneOrderInfo);

            var (goalText, tip) = await PhoneOrderTranscriptionAsync(phoneOrderInfo, record, audioContent, cancellationToken).ConfigureAwait(false);
            
            record.Tips = tip;
            record.ConversationText = goalText;
            
            await _phoneOrderDataProvider.UpdatePhoneOrderRecordsAsync(record, true, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            Log.Error("Extract phone order record error: {@Error}", e);
        }
    }

    public async Task<AddOrUpdateManualOrderResponse> AddOrUpdateManualOrderAsync(AddOrUpdateManualOrderCommand command, CancellationToken cancellationToken)
    {
        var orderId = long.Parse(command.OrderId);

        Log.Information($"Add manual order: {orderId}", orderId);

        var manualOrder = await _easyPosClient.GetOrderAsync(orderId, command.Restaurant, cancellationToken).ConfigureAwait(false);

        Log.Information("Get order response: response: {@manualOrder}", manualOrder);

        if (manualOrder.Data == null)
            return new AddOrUpdateManualOrderResponse
            {
                Msg = "pos not find order"
            };

        var items = await _phoneOrderDataProvider.GetPhoneOrderOrderItemsAsync(command.RecordId, PhoneOrderOrderType.ManualOrder, cancellationToken).ConfigureAwait(false);

        if (items is { Count: > 0 })
            await _phoneOrderDataProvider.DeletePhoneOrderItemsAsync(items, cancellationToken: cancellationToken).ConfigureAwait(false);

        var oderItems = manualOrder.Data.Order.OrderItems.Select(x =>
        {
            return new PhoneOrderOrderItem
            {
                Price = x.ItemAmount,
                Quantity = x.Quantity,
                RecordId = command.RecordId,
                OrderType = PhoneOrderOrderType.ManualOrder,
                Note = PickUpAnOrderNote(x),
                FoodName = x.Localizations.First(c => c.Field == "posName" && c.languageCode == "zh_CN").Value
            };
        }).ToList();

        var record = (await _phoneOrderDataProvider.GetPhoneOrderRecordAsync(command.RecordId, cancellationToken: cancellationToken).ConfigureAwait(false)).FirstOrDefault();

        record.ManualOrderId = orderId;

        await _phoneOrderDataProvider.UpdatePhoneOrderRecordsAsync(record, cancellationToken: cancellationToken).ConfigureAwait(false);

        await _phoneOrderDataProvider.AddPhoneOrderItemAsync(oderItems, cancellationToken: cancellationToken).ConfigureAwait(false);

        return new AddOrUpdateManualOrderResponse
        {
            Data = _mapper.Map<List<PhoneOrderOrderItemDto>>(oderItems)
        };
    }

    private static string PickUpAnOrderNote(EasyPosOrderItemDto item)
    {
        if (item.Condiments is { Count: 0 }) return null;

        var note = "";

        foreach (var condiment in item.Condiments)
        {
            if (condiment.ActionLocalizations is not { Count: 0 } && condiment.Localizations is not { Count: 0 })
                note = note + $"{condiment.ActionLocalizations.First(c => c.Field == "name" && c.languageCode == "zh_CN").Value + condiment.Localizations.First(c => c.Field == "name" && c.languageCode == "zh_CN").Value} (${condiment.Price})";
            else
                note = note + $"{condiment.Notes}(${condiment.Price})";
        }

        return note;
    }

    private async Task<List<SpeechMaticsSpeakInfoDto>> HandlerConversationFirstSentenceAsync(
        List<SpeechMaticsSpeakInfoDto> phoneOrderInfos, PhoneOrderRecord record, byte[] audioContent, CancellationToken cancellationToken)
    {
        var originText = await SplitAudioAsync(audioContent, record, phoneOrderInfos[0].StartTime * 1000,
            phoneOrderInfos[0].EndTime * 1000, TranscriptionFileType.Wav, cancellationToken).ConfigureAwait(false);

        if (await CheckAudioFirstSentenceIsRestaurantAsync(originText, cancellationToken).ConfigureAwait(false))
            return phoneOrderInfos;

        foreach (var phoneOrderInfo in phoneOrderInfos)
        {
            phoneOrderInfo.Speaker = phoneOrderInfo.Speaker == "S1" ? "S2" : "S1";
            phoneOrderInfo.Role = phoneOrderInfo.Role == PhoneOrderRole.Restaurant ? PhoneOrderRole.Client : PhoneOrderRole.Restaurant;
        }

        phoneOrderInfos.Insert(0, new SpeechMaticsSpeakInfoDto
        {
            StartTime = 0,
            EndTime = 0,
            Role = PhoneOrderRole.Restaurant,
            Speaker = "S1"
        });

        return phoneOrderInfos;
    }

    private async Task<(string, string)> PhoneOrderTranscriptionAsync(
        List<SpeechMaticsSpeakInfoDto> phoneOrderInfo, PhoneOrderRecord record, byte[] audioContent, CancellationToken cancellationToken)
    {
        var conversationIndex = 0;
        var goalTexts = new List<string>();
        var conversations = new List<PhoneOrderConversation>();

        foreach (var speakDetail in phoneOrderInfo)
        {
            Log.Information("Start time of speak in video: {SpeakStartTimeVideo}, End time of speak in video: {SpeakEndTimeVideo}", speakDetail.StartTime * 1000, speakDetail.EndTime * 1000);

            try
            {
                string originText;

                if (speakDetail.StartTime != 0 && speakDetail.EndTime != 0)
                    originText = await SplitAudioAsync(
                        audioContent, record, speakDetail.StartTime * 1000, speakDetail.EndTime * 1000, TranscriptionFileType.Wav, cancellationToken).ConfigureAwait(false);
                else
                    originText = "";

                Log.Information("Phone Order transcript originText: {originText}", originText);

                goalTexts.Add((speakDetail.Role == PhoneOrderRole.Restaurant
                    ? PhoneOrderRole.Restaurant.ToString()
                    : PhoneOrderRole.Client.ToString()) + ": " + originText);

                if (speakDetail.Role == PhoneOrderRole.Restaurant)
                    conversations.Add(new PhoneOrderConversation { RecordId = record.Id, Question = originText, Order = conversationIndex, StartTime = speakDetail.StartTime, EndTime = speakDetail.EndTime });
                else
                {
                    if (conversationIndex >= conversations.Count)
                        conversations.Add(new PhoneOrderConversation { RecordId = record.Id, Question = "", Order = conversationIndex, StartTime = speakDetail.StartTime, EndTime = speakDetail.EndTime });

                    conversations[conversationIndex].Answer = originText;
                    conversationIndex++;
                }
            }
            catch (Exception ex)
            {
                record.Status = PhoneOrderRecordStatus.Exception;

                Log.Information("transcription error: {ErrorMessage}", ex.Message);
            }
        }

        var goalTextsString = string.Join("\n", goalTexts);

        if (await CheckRestaurantRecordingRoleAsync(goalTextsString, cancellationToken).ConfigureAwait(false))
        {
            if (conversations[0].Question.IsNullOrEmpty())
            {
                conversations.Insert(0, new PhoneOrderConversation
                {
                    Order = 0,
                    Answer = "",
                    Question = "",
                    RecordId = record.Id,
                    StartTime = phoneOrderInfo.FirstOrDefault()?.StartTime ?? 0,
                    EndTime = phoneOrderInfo.FirstOrDefault()?.EndTime ?? 0
                });
            }

            ShiftConversations(conversations);
        }

        goalTextsString = ProcessConversation(conversations, goalTextsString);

        await _phoneOrderDataProvider.AddPhoneOrderConversationsAsync(conversations.Count != 0 ? conversations : 
        [
            new PhoneOrderConversation { Question = goalTextsString, Answer = string.Empty, RecordId = record.Id, Order = 0 }
        ], true, cancellationToken).ConfigureAwait(false);

        return (goalTextsString, conversations.FirstOrDefault()?.Question ?? goalTextsString);
    }

    private static string ProcessConversation(List<PhoneOrderConversation> conversations, string goalTextsString)
    {
        if (conversations == null || conversations.Count == 0) return goalTextsString;

        goalTextsString = "";

        foreach (var conversation in conversations.ToList())
        {
            if (string.IsNullOrEmpty(conversation.Answer) && string.IsNullOrEmpty(conversation.Question)) 
                conversations.Remove(conversation);
            else
            {
                if (string.IsNullOrEmpty(conversation.Answer))
                    conversation.Answer = string.Empty;

                if (string.IsNullOrEmpty(conversation.Question))
                    conversation.Question = string.Empty;

                goalTextsString = goalTextsString + "Restaurant: " + conversation.Question + "\nClient:" + conversation.Answer + "\n";
            }
        }

        Log.Information("Processed conversation:{@conversations}， goalText:{@goalTextsString}", conversations, goalTextsString);

        return goalTextsString;
    }

    private async Task<bool> CheckAudioFirstSentenceIsRestaurantAsync(string query, CancellationToken cancellationToken)
    {
        var completionResult = await _smartiesClient.PerformQueryAsync(new AskGptRequest
        {
            Messages = new List<CompletionsRequestMessageDto>
            {
                new()
                {
                    Role = "system",
                    Content = new CompletionsStringContent("你是一款餐厅订餐语句高度理解的智能助手，专门用于分辨语句是顾客还是餐厅说的。" +
                                                           "请根据我提供的语句，判断语句是属于餐厅还是顾客说的，如果是餐厅的话，请返回\"true\"，如果是顾客的话，请返回\"false\"，" +
                                                           "注意:\n" +
                                                           "1. 如果语句中没有提及餐馆或或者点餐意图的，只是单纯的打招呼，则判断为餐厅说的，返回true。\n" +
                                                           "2. 如果是比较短的语句且是一些莫名其妙的字眼，例如Hamras（实际是Hello, Moon house），可以判断是餐厅说的\n" +
                                                           "- 样本与输出：\n" +
                                                           "input:你好,江南春 output:true\n" +
                                                           "input:hello,MoonHouse output:true\n" +
                                                           "input:你好,湘里人家 output:true\n" +
                                                           "input:喂,out:true\n" +
                                                           "input:Hamras, output:true" +
                                                           "input:你好，我要点单 output:false\n" +
                                                           "input:你好，这里是江南春吗 output:false\n" +
                                                           "input:你好，我是小明，我可以订餐吗 output:false")
                },
                new()
                {
                    Role = "user",
                    Content = new CompletionsStringContent($"input: {query}, output:")
                }
            },
            Model = OpenAiModel.Gpt4o
        }, cancellationToken).ConfigureAwait(false);

        return bool.Parse(completionResult.Data.Response);
    }

    private async Task<bool> CheckRestaurantRecordingRoleAsync(string query, CancellationToken cancellationToken)
    {
        var completionResult = await _smartiesClient.PerformQueryAsync(new AskGptRequest
        {
            Messages = new List<CompletionsRequestMessageDto>
            {
                new()
                {
                    Role = "system",
                    Content = new CompletionsStringContent("你是一款高度智能的餐厅订餐语句理解助手，专门用于分辨对话中角色的言辞是否符合其身份。" +
                                                           "请基于以下规则判断对话角色是否正确匹配：\n" +
                                                           "1.结合上下文通篇理解，判断角色是否存在颠倒 \n" +
                                                           "2.提出咨询客人信息以及提供餐厅信息的一般是餐厅(Restaurant) \n" +
                                                           "3.咨询菜品、下单、营业时间的语句一般为客人(Client) \n" +
                                                           "4.当颠倒占比大于75%就需要返回true \n" +
                                                           "- 样本与输出：\n" +
                                                           "input:Restaurant: Client: 你能告诉我今天的营业时间吗？Restaurant: 我想点两份牛排和一份沙拉。Client: 我们的牛排是鲜嫩多汁的，适合搭配红酒。Restaurant: 请告诉我您的联系方式。Client: 好的，我的电话是12345678。output:true\n" +
                                                           "input:Restaurant: Client: 我想预定今晚8点的晚餐。Restaurant: 好的，请告诉我您的联系方式。Client: 我的电话是12345678。Restaurant: 今天有什么推荐菜吗？Client: 我们有牛排和烤鸡，特别受欢迎。output:false\n" +
                                                           "input:Restaurant: . Client: 请问你们什么时候打烊？Restaurant: 我们晚上10点打烊。Client: 我想预定一份牛排和一份意面。Restaurant: 好的，请问您的联系方式？output:false\n")
                },
                new()
                {
                    Role = "user",
                    Content = new CompletionsStringContent($"input: {query}, output:")
                }
            },
            Model = OpenAiModel.Gpt4o
        }, cancellationToken).ConfigureAwait(false);

        return bool.TryParse(completionResult.Data.Response, out var result) && result;
    }

    private static void ShiftConversations(List<PhoneOrderConversation> conversations)
    {
        Log.Information("Before shift conversations: {@conversations}", conversations);

        for (var i = 0; i < conversations.Count - 1; i++)
        {
            var currentConversation = conversations[i];
            var nextConversation = conversations[i + 1];

            currentConversation.Question = currentConversation.Answer;
            currentConversation.Answer = nextConversation.Question;
            currentConversation.Order = i;
            currentConversation.StartTime = currentConversation.EndTime;
            currentConversation.EndTime = nextConversation.StartTime;
        }

        var lastConversation = conversations[^1];
        lastConversation.Question = lastConversation.Answer;
        lastConversation.Answer = null;
        lastConversation.Order = conversations.Count - 1;
        lastConversation.StartTime = lastConversation.EndTime;
        lastConversation.EndTime = null;

        Log.Information("After shift conversations: {@conversations}", conversations);
    }

    private async Task AddPhoneOrderRecordAsync(PhoneOrderRecord record, PhoneOrderRecordStatus status, CancellationToken cancellationToken)
    {
        record.Status = status;

        await _phoneOrderDataProvider.AddPhoneOrderRecordsAsync(new List<PhoneOrderRecord> { record }, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        Log.Information("Added phone order record: {@record}", record);
    }

    private async Task<bool> CheckPhoneOrderRecordDurationAsync(byte[] recordContent, CancellationToken cancellationToken)
    {
        var audioDuration = await _ffmpegService.GetAudioDurationAsync(recordContent, cancellationToken).ConfigureAwait(false);

        Log.Information($"Phone order record audio duration: {audioDuration}", audioDuration);

        var timeSpan = TimeSpan.Parse(audioDuration);

        return timeSpan.TotalSeconds < 15 && (timeSpan.TotalSeconds < 3 || timeSpan.Seconds == 14 || timeSpan.Seconds == 10);
    }

    private async Task<string> UploadRecordFileAsync(string fileName, byte[] fileContent, CancellationToken cancellationToken)
    {
        var uploadResponse = await _attachmentService.UploadAttachmentAsync(new UploadAttachmentCommand
        {
            Attachment = new UploadAttachmentDto
            {
                FileName = fileName,
                FileContent = fileContent
            }
        }, cancellationToken).ConfigureAwait(false);

        return uploadResponse.Attachment.FileUrl;
    }

    private async Task<PhoneOrderRecordInformationDto> ExtractPhoneOrderRecordInfoAsync(string recordName, int agentId, DateTimeOffset? startTime, CancellationToken cancellationToken)
    {
        var agent = await _agentDataProvider.GetAgentByIdAsync(agentId, cancellationToken: cancellationToken).ConfigureAwait(false);

        var phoneNumber = TryExtractTargetNumber(recordName);
        
        return new PhoneOrderRecordInformationDto
        {
            Agent = _mapper.Map<AgentDto>(agent),
            StartDate = startTime ?? ExtractPhoneOrderStartDateFromRecordName(recordName),
            PhoneNumber = phoneNumber
        };
    }
    
    private static string TryExtractTargetNumber(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return "";

        var parts = fileName.Split('-', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 3)
            return "";

        return parts[0] switch
        {
            "out" when parts.Length > 1 => parts[1],
            _ => ""
        };
    }

    private DateTimeOffset ExtractPhoneOrderStartDateFromRecordName(string recordName)
    {
        var time = string.Empty;

        var regexInOut = new Regex(@"-(\d+)\.");
        var match = regexInOut.Match(recordName);

        if (match.Success) time = match.Groups[1].Value;

        return TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeSeconds(long.Parse(time)), TimeZoneInfo.FindSystemTimeZoneById("America/Los_Angeles"));
    }

    private async Task UpdatePhoneOrderRecordSpecificFieldsAsync(int recordId, int modifiedBy, string tips, string lastModifiedByName, CancellationToken cancellationToken)
    {
        var record = (await _phoneOrderDataProvider.GetPhoneOrderRecordAsync(recordId, cancellationToken: cancellationToken).ConfigureAwait(false)).FirstOrDefault();

        if (record == null) return;

        record.Tips = tips;
        record.LastModifiedBy = modifiedBy;
        record.LastModifiedDate = DateTimeOffset.Now;
        record.LastModifiedByName = lastModifiedByName;

        await _phoneOrderDataProvider.UpdatePhoneOrderRecordsAsync(record, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task<string> SplitAudioAsync(
        byte[] file, PhoneOrderRecord record, double speakStartTimeVideo, double speakEndTimeVideo, TranscriptionFileType fileType = TranscriptionFileType.Wav, CancellationToken cancellationToken = default)
    {
        if (file == null) return null;

        var audioBytes = await _ffmpegService.ConvertFileFormatAsync(file, fileType, cancellationToken).ConfigureAwait(false);

        var splitAudios = await _ffmpegService.SpiltAudioAsync(audioBytes, speakStartTimeVideo, speakEndTimeVideo, cancellationToken: cancellationToken).ConfigureAwait(false);

        var transcriptionResult = new StringBuilder();
        
        try
        {
            var transcriptionResponse = await _speechToTextService.SpeechToTextAsync(
                splitAudios, record.Language, TranscriptionFileType.Wav, TranscriptionResponseFormat.Text,
                record.RestaurantInfo?.Message ?? string.Empty, cancellationToken: cancellationToken).ConfigureAwait(false);
                
            transcriptionResult.Append(transcriptionResponse);
        }
        catch (Exception e)
        {
            Log.Warning("Audio segment transcription error: {@Exception}", e);
        }

        Log.Information("Transcription result {Transcription}", transcriptionResult.ToString());

        return transcriptionResult.ToString();
    }
    
    private (DateTimeOffset? UtcStart, DateTimeOffset? UtcEnd) ConvertPstDateToUtcRange(DateTimeOffset? inputDate)
    {
        if (!inputDate.HasValue) return (null, null);

        var pstTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");

        var pstDate = new DateTime(inputDate.Value.Year, inputDate.Value.Month, inputDate.Value.Day, 0, 0, 0);
        var pstStart = new DateTimeOffset(pstDate, pstTimeZone.GetUtcOffset(pstDate));

        var utcStart = pstStart.ToUniversalTime();
        var utcEnd = utcStart.AddDays(1);

        return (utcStart, utcEnd);
    }

    public async Task<GetPhoneCallUsagesPreviewResponse> GetPhoneCallUsagesPreviewAsync(GetPhoneCallUsagesPreviewRequest request, CancellationToken cancellationToken)
    {
        var (startTime, endTime) = GetQueryTimeRange(request.Month);

        var result = await _phoneOrderDataProvider
            .GetPhonCallUsagesAsync(startTime, endTime, request.IncludeExternalData, cancellationToken).ConfigureAwait(false);

        var data = result.GroupBy(x => x.Record.AgentId).Select(x => new PhoneCallUsagesPreviewDto
        {
            Name = x.First().Assistant?.Name,
            ReportUsages = x.Where(r => !string.IsNullOrWhiteSpace(r.Record.TranscriptionText)).Count(),
            TotalDuration = Math.Round(x.Where(r => r.Record.Duration != null).Select(r => r.Record.Duration.Value).Sum(), 2)
        }).ToList();
        
        return new GetPhoneCallUsagesPreviewResponse { Data = data };
    }
    
    public async Task<GetPhoneCallRecordDetailResponse> GetPhoneCallrecordDetailAsync(GetPhoneCallRecordDetailRequest request, CancellationToken cancellationToken)
    {
        var (startTime, endTime) = GetQueryTimeRange(request.Month);

        var result = await _phoneOrderDataProvider
            .GetPhonCallUsagesAsync(startTime, endTime, request.IncludeExternalData, cancellationToken).ConfigureAwait(false);

        var data = result.Where(r => !string.IsNullOrEmpty(r.Record.Url)).OrderBy(r => r.Record.CreatedDate).Select(x =>
            new PhoneCallRecordDetailDto
            {
                Name = x.Assistant?.Name,
                Url = x.Record.Url,
                Duration = x.Record.Duration,
                PhoneNumber = x.Record.IncomingCallNumber ?? string.Empty,
                InBoundType = x.Record.Url.Contains("twilio") ? "電話" : "網頁",
                CreatedDate = ConvertUtcToPst(x.Record.CreatedDate),
                IsTransfer = x.Record.IsTransfer.HasValue ? x.Record.IsTransfer.Value ? "是" : "" : ""
            }).ToList();
        
        var fileUrl = await ToExcelTransposed(data, cancellationToken).ConfigureAwait(false);

        return new GetPhoneCallRecordDetailResponse { Data = fileUrl };
    }

    public async Task<GetPhoneOrderCompanyCallReportResponse> GetPhoneOrderCompanyCallReportAsync(GetPhoneOrderCompanyCallReportRequest request, CancellationToken cancellationToken)
    {
        var companyName = _salesSetting.CompanyName?.Trim();
        if (string.IsNullOrWhiteSpace(companyName))
            throw new Exception("Sales CompanyName is not configured.");

        var company = await _posDataProvider.GetPosCompanyByNameAsync(companyName, cancellationToken).ConfigureAwait(false);
        if (company == null)
            return new GetPhoneOrderCompanyCallReportResponse { Data = string.Empty };

        var assistantIds = await _posDataProvider.GetAssistantIdsByCompanyIdAsync(company.Id, cancellationToken).ConfigureAwait(false);
        const int daysWindow = 30;
        var latestRecords = await _phoneOrderDataProvider
            .GetLatestPhoneOrderRecordsByAssistantIdsAsync(assistantIds, daysWindow, cancellationToken)
            .ConfigureAwait(false);

        var assistantNameMap = new Dictionary<int, string>();
        var assistantLanguageMap = new Dictionary<int, string>();
        if (assistantIds.Count > 0)
        {
            var assistants = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantByIdsAsync(assistantIds, cancellationToken).ConfigureAwait(false);
            assistantNameMap = assistants
                .GroupBy(x => x.Id)
                .ToDictionary(g => g.Key, g => g.First().Name ?? string.Empty);
            assistantLanguageMap = assistants
                .GroupBy(x => x.Id)
                .ToDictionary(g => g.Key, g => g.First().Language ?? string.Empty);
        }

        var (utcStart, utcEnd) = GetCompanyCallReportUtcRange(request.ReportType);

        var records = assistantIds.Count == 0
            ? []
            : await _phoneOrderDataProvider.GetPhoneOrderRecordsByAssistantIdsAsync(assistantIds, utcStart, utcEnd, cancellationToken).ConfigureAwait(false);

        var reportRows = BuildCompanyCallReportRows(records, assistantNameMap, assistantLanguageMap, latestRecords, daysWindow);
        var fileUrl = await ToCompanyCallReportExcelAsync(reportRows, request.ReportType, cancellationToken).ConfigureAwait(false);

        return new GetPhoneOrderCompanyCallReportResponse { Data = fileUrl };
    }

    public async Task<GetPhoneOrderRecordReportResponse> GetPhoneOrderRecordReportByCallSidAsync(GetPhoneOrderRecordReportRequest request, CancellationToken cancellationToken)
    {
        var report = await _phoneOrderDataProvider.GetPhoneOrderRecordReportAsync(request.CallSid, request.Language, cancellationToken).ConfigureAwait(false);
        
        if (report == null)
        {
            var record = await _phoneOrderDataProvider.GetPhoneOrderRecordBySessionIdAsync(request.CallSid, cancellationToken).ConfigureAwait(false);

            var newReport = new PhoneOrderRecordReportDto()
            {
                RecordId = record.Id,
                Language = (TranscriptionLanguage)request.Language,
                Report = record.TranscriptionText,
                IsOrigin = (TranscriptionLanguage)request.Language == record.Language,
            };

            return new GetPhoneOrderRecordReportResponse()
            {
                Data = newReport
            };
        }

        if (report == null)
        {
            var record = await _phoneOrderDataProvider.GetPhoneOrderRecordBySessionIdAsync(request.CallSid, cancellationToken).ConfigureAwait(false);

            var newReport = new PhoneOrderRecordReportDto()
            {
                RecordId = record.Id,
                Language = (TranscriptionLanguage)request.Language,
                Report = record.TranscriptionText,
                IsOrigin = (TranscriptionLanguage)request.Language == record.Language,
            };

            return new GetPhoneOrderRecordReportResponse()
            {
                Data = newReport
            };
        }

        return new GetPhoneOrderRecordReportResponse()
        {
            Data = _mapper.Map<PhoneOrderRecordReportDto>(report)
        };
    }

    private static List<CompanyCallReportRow> BuildCompanyCallReportRows(
        List<PhoneOrderRecord> records,
        IReadOnlyDictionary<int, string> assistantNameMap,
        IReadOnlyDictionary<int, string> assistantLanguageMap,
        IReadOnlyDictionary<int, PhoneOrderRecord> latestRecords,
        int daysWindow)
    {
        if (records == null || records.Count == 0) return [];

        var chinaZone = GetChinaTimeZone();
        var nowChina = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, chinaZone);
        var todayLocal = new DateTime(nowChina.Year, nowChina.Month, nowChina.Day, 0, 0, 0, DateTimeKind.Unspecified);

        return records
            .Where(record => record.AssistantId.HasValue)
            .GroupBy(record => record.AssistantId.Value)
            .Select(group =>
            {
                assistantNameMap.TryGetValue(group.Key, out var assistantName);
                assistantLanguageMap.TryGetValue(group.Key, out var assistantLanguage);
                latestRecords.TryGetValue(group.Key, out var latestRecord);

                var daysSinceLastCallText = latestRecord == null
                    ? $"超过{daysWindow}天"
                    : CalculateDaysSinceLastCallText(latestRecord.CreatedDate, todayLocal, chinaZone);

                return new CompanyCallReportRow
                {
                    CustomerId = string.IsNullOrWhiteSpace(assistantName) ? group.Key.ToString() : assistantName,
                    CustomerLanguage = assistantLanguage ?? string.Empty,
                    TotalCalls = group.Count(),
                    OrderCount = group.Count(x => x.Scenario == DialogueScenarios.Order),
                    TransferCount = group.Count(x => x.Scenario == DialogueScenarios.TransferToHuman),
                    ComplaintCount = group.Count(x => x.Scenario == DialogueScenarios.ComplaintFeedback),
                    SalesCount = group.Count(x => x.Scenario == DialogueScenarios.SalesCall),
                    InvalidCount = group.Count(x => x.Scenario == DialogueScenarios.InvalidCall),
                    InquiryCount = group.Count(x => x.Scenario == DialogueScenarios.Inquiry),
                    DaysSinceLastCallText = daysSinceLastCallText
                };
            })
            .OrderBy(row => row.CustomerId)
            .ToList();
    }

    private (DateTimeOffset StartUtc, DateTimeOffset EndUtc) GetQueryTimeRange(int month)
    {
        if (month < 1 || month > 12) throw new ArgumentOutOfRangeException(nameof(month));

        var tz = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time"); // PT, 含 DST

        var startLocal = new DateTime(2025, month, 1, 0, 0, 0, DateTimeKind.Unspecified);

        var nextMonthLocal = (month == 12)
            ? new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified)
            : new DateTime(2025, month + 1, 1, 0, 0, 0, DateTimeKind.Unspecified);

        var startUtc = TimeZoneInfo.ConvertTimeToUtc(startLocal, tz);
        var endUtc = TimeZoneInfo.ConvertTimeToUtc(nextMonthLocal, tz);

        return (new DateTimeOffset(startUtc), new DateTimeOffset(endUtc));
    }

    private static (DateTimeOffset StartUtc, DateTimeOffset EndUtc) GetCompanyCallReportUtcRange(PhoneOrderCallReportType reportType)
    {
        var chinaZone = GetChinaTimeZone();
        var nowChina = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, chinaZone);
        var todayLocal = new DateTime(nowChina.Year, nowChina.Month, nowChina.Day, 0, 0, 0, DateTimeKind.Unspecified);

        if (reportType == PhoneOrderCallReportType.Daily)
        {
            var startUtc = TimeZoneInfo.ConvertTimeToUtc(todayLocal, chinaZone);
            var endUtc = startUtc.AddDays(1);

            return (new DateTimeOffset(startUtc), new DateTimeOffset(endUtc));
        }

        var startOfWeekLocal = todayLocal.AddDays(-((int)todayLocal.DayOfWeek + 6) % 7);
        var weekStartUtc = TimeZoneInfo.ConvertTimeToUtc(startOfWeekLocal, chinaZone);
        var weekEndUtc = weekStartUtc.AddDays(7);

        return (new DateTimeOffset(weekStartUtc), new DateTimeOffset(weekEndUtc));
    }

    private static TimeZoneInfo GetChinaTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("China Standard Time");
        }
    }

    private static string CalculateDaysSinceLastCallText(DateTimeOffset latestCallUtc, DateTime todayLocal, TimeZoneInfo chinaZone)
    {
        var latestLocal = TimeZoneInfo.ConvertTime(latestCallUtc, chinaZone);
        var diff = todayLocal - latestLocal.DateTime;
        var days = Math.Max(0, Math.Round(diff.TotalDays, 1));

        return days.ToString("0.0");
    }
    
    private string ConvertUtcToPst(DateTimeOffset utcTime)
    {
        var pstTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
        
        var pstTime = TimeZoneInfo.ConvertTimeFromUtc(utcTime.UtcDateTime, pstTimeZone);
        
        var offset = pstTimeZone.GetUtcOffset(pstTime);
        
        return new DateTimeOffset(pstTime, offset).ToString("yyyy-MM-dd HH:mm:ss");
    }
    
     /// <summary>
    /// 把任意 List&lt;T&gt; 导出为 Excel，布局为：
    /// 第一列按行列出每个属性的 Json 名（优先取 JsonProperty / JsonPropertyName），
    /// 后续每一列是 list 中一个元素，单元格是该属性在该元素上的值（复杂对象 JSON 序列化）。
    /// 返回生成的 .xlsx 的字节数组。
    /// </summary>
    private async Task<string> ToExcelTransposed<T>(IList<T> list, CancellationToken cancellationToken)
    {
        if (list == null) throw new ArgumentNullException(nameof(list));
        var type = typeof(T);

        // 取公共可读属性
        var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanRead).ToArray();

        // 解析每个属性的“名字”：优先 JsonProperty（Newtonsoft），其次 JsonPropertyName（System.Text.Json），否则原始属性名
        var fieldNames = props.Select(p =>
        {
            var newtonAttr = p.GetCustomAttribute<JsonPropertyAttribute>();
            if (newtonAttr != null && !string.IsNullOrWhiteSpace(newtonAttr.PropertyName))
                return newtonAttr.PropertyName;

            var systemAttr = p.GetCustomAttribute<JsonPropertyNameAttribute>();
            if (systemAttr != null && !string.IsNullOrWhiteSpace(systemAttr.Name))
                return systemAttr.Name;

            return p.Name;
        }).ToArray();

        using var workbook = new XLWorkbook();
        var ws = workbook.AddWorksheet("Sheet1");

        // 顶部
        for (var row = 0; row < fieldNames.Length; row++)
        {
            ws.Cell(1, row + 1).Value = fieldNames[row];
        }

        // 填充每个元素对应的值（复杂对象 JSON 序列化）
        for (var row = 0; row < list.Count; row++)
        {
            var item = list[row];
            for (var col = 0; col < props.Length; col++)
            {
                var prop = props[col];
                var value = prop.GetValue(item);
                string cellValue;

                if (value == null)
                {
                    cellValue = string.Empty;
                }
                else if (value is double d)
                {
                    cellValue = d.ToString("0.00");
                }
                else if (IsSimpleType(value.GetType()))
                {
                    cellValue = value.ToString()!;
                }
                else
                {
                    cellValue = JsonConvert.SerializeObject(value);
                }

                ws.Cell(row + 2, col + 1).Value = cellValue;
            }
        }

        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        
        var audio = await _attachmentService.UploadAttachmentAsync(new UploadAttachmentCommand
        {
            Attachment = new UploadAttachmentDto { FileName = Guid.NewGuid() + ".xlsx", FileContent = ms.ToArray() }
        }, cancellationToken).ConfigureAwait(false);
        
        return audio.Attachment?.FileUrl ?? string.Empty;
    }

    private async Task<string> ToCompanyCallReportExcelAsync(
        IReadOnlyList<CompanyCallReportRow> rows, PhoneOrderCallReportType reportType, CancellationToken cancellationToken)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.AddWorksheet("Sheet1");

        var headers = reportType == PhoneOrderCallReportType.Daily
            ? new[]
            {
                "customer id",
                "客人語種",
                "當日有效通話量合計（所有通話-無效通話）",
                "當日下單",
                "當日轉接",
                "當日投訴",
                "當天推銷",
                "當日無效",
                "多久沒來電"
            }
            : new[]
            {
                "customer id",
                "客人語種",
                "本周有call入 Sales",
                "本周有效通話量（下单+转接+咨询）",
                "本周下單",
                "本周轉接",
                "本周投訴",
                "本周推銷",
                "本周無效"
            };

        for (var col = 0; col < headers.Length; col++)
            ws.Cell(1, col + 1).Value = headers[col];

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];
            var colIndex = 1;

            ws.Cell(rowIndex + 2, colIndex++).Value = row.CustomerId;
            ws.Cell(rowIndex + 2, colIndex++).Value = row.CustomerLanguage ?? string.Empty;

            if (reportType == PhoneOrderCallReportType.Daily)
            {
                ws.Cell(rowIndex + 2, colIndex++).Value = row.TotalCalls - row.InvalidCount;
                ws.Cell(rowIndex + 2, colIndex++).Value = row.OrderCount;
                ws.Cell(rowIndex + 2, colIndex++).Value = row.TransferCount;
                ws.Cell(rowIndex + 2, colIndex++).Value = row.ComplaintCount;
                ws.Cell(rowIndex + 2, colIndex++).Value = row.SalesCount;
                ws.Cell(rowIndex + 2, colIndex++).Value = row.InvalidCount;
                ws.Cell(rowIndex + 2, colIndex).Value = row.DaysSinceLastCallText ?? string.Empty;
            }
            else
            {
                ws.Cell(rowIndex + 2, colIndex++).Value = row.TotalCalls;
                ws.Cell(rowIndex + 2, colIndex++).Value = row.OrderCount + row.TransferCount + row.InquiryCount;
                ws.Cell(rowIndex + 2, colIndex++).Value = row.OrderCount;
                ws.Cell(rowIndex + 2, colIndex++).Value = row.TransferCount;
                ws.Cell(rowIndex + 2, colIndex++).Value = row.ComplaintCount;
                ws.Cell(rowIndex + 2, colIndex++).Value = row.SalesCount;
                ws.Cell(rowIndex + 2, colIndex).Value = row.InvalidCount;
            }
        }

        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);

        var attachment = await _attachmentService.UploadAttachmentAsync(new UploadAttachmentCommand
        {
            Attachment = new UploadAttachmentDto { FileName = Guid.NewGuid() + ".xlsx", FileContent = ms.ToArray() }
        }, cancellationToken).ConfigureAwait(false);

        return attachment.Attachment?.FileUrl ?? string.Empty;
    }

    /// <summary>
    /// 判断是否是简单类型（可以原样 ToString），否则认为复杂需要 JSON 序列化
    /// </summary>
    private static bool IsSimpleType(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;

        return
            type.IsPrimitive ||
            type.IsEnum ||
            type == typeof(string) ||
            type == typeof(decimal) ||
            type == typeof(DateTime) ||
            type == typeof(DateTimeOffset) ||
            type == typeof(Guid) ||
            type == typeof(TimeSpan);
    }

    private sealed class CompanyCallReportRow
    {
        public string CustomerId { get; set; }

        public string CustomerLanguage { get; set; }

        public int TotalCalls { get; set; }

        public int OrderCount { get; set; }

        public int TransferCount { get; set; }

        public int ComplaintCount { get; set; }

        public int SalesCount { get; set; }

        public int InvalidCount { get; set; }

        public int InquiryCount { get; set; }

        public string DaysSinceLastCallText { get; set; }
    }
    
    public async Task<GetPhoneOrderDataDashboardResponse> GetPhoneOrderDataDashboardAsync(GetPhoneOrderDataDashboardRequest request, CancellationToken cancellationToken)
    {
        var targetOffset = request.StartDate.Offset;
        var utcStart = request.StartDate.ToUniversalTime();
        var utcEnd = request.EndDate.ToUniversalTime();

        Log.Information("[PhoneDashboard] Fetch phone order records: Agents={@AgentIds}, Range={@Start}-{@End} (UTC: {@UtcStart}-{@UtcEnd})", request.AgentIds, request.StartDate, request.EndDate, utcStart, utcEnd);
        
        var records = await _phoneOrderDataProvider.GetPhoneOrderRecordsByAgentIdsAsync(agentIds: request.AgentIds, utcStart: utcStart, utcEnd: utcEnd, cancellationToken: cancellationToken).ConfigureAwait(false);

        Log.Information("[PhoneDashboard] Phone order records fetched: {@Count}", records?.Count ?? 0);
        
        var posOrders = await _posDataProvider.GetPosOrdersByStoreIdsAsync(request.StoreIds, null, true, utcStart, utcEnd, cancellationToken).ConfigureAwait(false);
        var cancelledOrders = await _posDataProvider.GetPosOrdersByStoreIdsAsync(request.StoreIds, PosOrderModifiedStatus.Cancelled, true, utcStart, utcEnd, cancellationToken).ConfigureAwait(false);

        Log.Information("[PhoneDashboard] POS orders loaded: Total={@Total}, Cancelled={@Cancelled}", posOrders.Count, cancelledOrders.Count);
              
        var orderCountPerPeriod = GroupCountByRequestType(posOrders, x => x.CreatedDate.ToOffset(targetOffset), request.StartDate, request.EndDate, request.DataType);
        var cancelledOrderCountPerPeriod = GroupCountByRequestType(cancelledOrders, x => x.CreatedDate.ToOffset(targetOffset), request.StartDate, request.EndDate, request.DataType);
        
        var restaurantData = new RestaurantDataDto
        {
            OrderCount = posOrders.Count,
            TotalOrderAmount = posOrders.Sum(x => x.Total) - cancelledOrders.Sum(x => x.Total),
            CancelledOrderCount = cancelledOrders.Count,
            OrderCountPerPeriod = orderCountPerPeriod,
            CancelledOrderCountPerPeriod = cancelledOrderCountPerPeriod
        };
        
        var callInRecords = records?.Where(x => x.OrderRecordType == PhoneOrderRecordType.InBound).ToList() ?? new List<PhoneOrderRecord>();
        var callOutRecords = records?.Where(x => x.OrderRecordType == PhoneOrderRecordType.OutBount).ToList() ?? new List<PhoneOrderRecord>();

        var callInFailedCount = records?.Count(x => x.OrderRecordType == PhoneOrderRecordType.InBound && x.Scenario is DialogueScenarios.TransferVoicemail or DialogueScenarios.InvalidCall) ?? 0;

        var callOutFailedCount = records?.Count(x => x.OrderRecordType == PhoneOrderRecordType.OutBount && x.Scenario is DialogueScenarios.TransferVoicemail or DialogueScenarios.InvalidCall) ?? 0;
        
        Log.Information("[PhoneDashboard] Phone order Failed Count CallIn={@callInFailedCount}, CallOut={@callOutFailedCount}", callInFailedCount, callOutFailedCount);
        
        callInRecords.ForEach(r => r.CreatedDate = r.CreatedDate.ToOffset(targetOffset));
        callOutRecords.ForEach(r => r.CreatedDate = r.CreatedDate.ToOffset(targetOffset));

        Log.Information("[PhoneDashboard] Phone order records loaded: CallIn={@CallIn}, CallOut={@CallOut}", callInRecords.Count, callOutRecords.Count);
        
        var callInData = BuildCallInData(callInRecords, callInFailedCount, request.InvalidCallSeconds, request.StartDate, request.EndDate, request.DataType);
        var callOutData = BuildCallOutData(callOutRecords, callOutFailedCount, request.InvalidCallSeconds, request.StartDate, request.EndDate, request.DataType);
   
        await ApplyPeriodComparisonAsync(request, callInRecords, callOutRecords, restaurantData, callInData, callOutData, cancellationToken).ConfigureAwait(false);

        return new GetPhoneOrderDataDashboardResponse
        {
            Data = new GetPhoneOrderDataDashboardResponseData
            {
                CallInData = callInData,
                CallOutData = callOutData,
                Restaurant = restaurantData
            }
        };
    }

    private static CallInDataDto BuildCallInData(List<PhoneOrderRecord> callInRecords, int callInFailedCount, int? invalidCallSeconds, DateTimeOffset? start, DateTimeOffset? end, PhoneOrderDataDashDataType dataType)
    {
        var answeredCount = callInRecords.Count;

        var totalRepeatCalls = callInRecords.GroupBy(x => x.PhoneNumber).Select(g => Math.Max(0, g.Count() - 1)).Sum();

        var effectiveCount = answeredCount - callInRecords.Count(x => (x.Duration ?? 0) <= invalidCallSeconds);
        var averageDuration = callInRecords.DefaultIfEmpty().Average(x => x?.Duration ?? 0);
        var totalDuration = callInRecords.Sum(x => x.Duration ?? 0);
        var friendlyCount = callInRecords.Count(x => x.IsCustomerFriendly == true);
        var satisfactionRate = answeredCount > 0 ? (double)friendlyCount / answeredCount : 0;
        var transferCount = callInRecords.Count(x => x.IsTransfer == true || x.Scenario == DialogueScenarios.TransferToHuman);
        var transferRate = answeredCount > 0 ? (double)transferCount / answeredCount : 0;
        var repeatRate = answeredCount > 0 ? (double)totalRepeatCalls / answeredCount : 0;

        var totalDurationPerPeriod = GroupDurationByRequestType(callInRecords, start, end, dataType);

        return new CallInDataDto
        {
            AnsweredCallInCount = answeredCount,
            AverageCallInDurationSeconds = averageDuration,
            CallInAnsweredByHumanCount = transferCount,
            EffectiveCommunicationCallInCount = effectiveCount,
            RepeatCallInRate = repeatRate,
            CallInSatisfactionRate = satisfactionRate,
            CallInMissedByHumanCount = callInFailedCount,
            CallinTransferToHumanRate = transferRate,
            TotalCallInDurationSeconds = totalDuration,
            TotalCallInDurationPerPeriod = totalDurationPerPeriod
        };
    }

    private static CallOutDataDto BuildCallOutData(List<PhoneOrderRecord> callOutRecords, int callInFailedCount, int? invalidCallSeconds, DateTimeOffset? start, DateTimeOffset? end, PhoneOrderDataDashDataType dataType)
    {
        var answeredCount = callOutRecords.Count;
        var effectiveCount = answeredCount - callOutRecords.Count(x => (x.Duration ?? 0) <= invalidCallSeconds);
        var averageDuration = callOutRecords.DefaultIfEmpty().Average(x => x?.Duration ?? 0);
        var totalDuration = callOutRecords.Sum(x => x.Duration ?? 0);
        var friendlyCount = callOutRecords.Count(x => x.IsCustomerFriendly == true);
        var satisfactionRate = answeredCount > 0 ? (double)friendlyCount / answeredCount : 0;
        var humanAnswerCount = callOutRecords.Count(x => x.IsHumanAnswered == true);

        var totalDurationPerPeriod = GroupDurationByRequestType(callOutRecords, start, end, dataType);

        return new CallOutDataDto
        {
            AnsweredCallOutCount = answeredCount,
            AverageCallOutDurationSeconds = averageDuration,
            EffectiveCommunicationCallOutCount = effectiveCount,
            CallOutNotAnsweredCount = callInFailedCount,
            CallOutAnsweredByHumanCount = humanAnswerCount,
            CallOutSatisfactionRate = satisfactionRate,
            TotalCallOutDurationSeconds = totalDuration,
            TotalCallOutDurationPerPeriod = totalDurationPerPeriod
        };
    }
    
    private static Dictionary<string, double> GroupDurationByRequestType(List<PhoneOrderRecord> records, DateTimeOffset? startDate, DateTimeOffset? endDate, PhoneOrderDataDashDataType dataType)
    {
        if (startDate == null || endDate == null) return new Dictionary<string, double>();
        
        var localStart = startDate.Value.ToOffset(startDate.Value.Offset);
        var localEnd = endDate.Value.ToOffset(startDate.Value.Offset);

        var filteredRecords = records.Where(x =>
            {
                var localCreated = x.CreatedDate.ToOffset(startDate.Value.Offset);
                return localCreated >= localStart && localCreated <= localEnd;
            }).ToList();
        
        if (dataType == PhoneOrderDataDashDataType.Month)
        {
            return filteredRecords
                .GroupBy(x =>
                {
                    var localDate = x.CreatedDate.ToOffset(startDate.Value.Offset);
                    return new { localDate.Year, localDate.Month };
                })
                .OrderBy(g => g.Key.Year)
                .ThenBy(g => g.Key.Month)
                .ToDictionary(
                    g => $"{g.Key.Year:D4}-{g.Key.Month:D2}",
                    g => g.Sum(x => x.Duration ?? 0));
        }
        
        return filteredRecords
            .GroupBy(x => x.CreatedDate.ToOffset(startDate.Value.Offset).Date)
            .OrderBy(g => g.Key)
            .ToDictionary(
                g => g.Key.ToString("yyyy-MM-dd"),
                g => g.Sum(x => x.Duration ?? 0));
    }
    
    private static Dictionary<string, int> GroupCountByRequestType(List<PosOrder> orders, Func<PosOrder, DateTimeOffset> dateSelector, DateTimeOffset? startDate, DateTimeOffset? endDate, PhoneOrderDataDashDataType dataType)
    {
        if (startDate == null || endDate == null || orders == null) return new Dictionary<string, int>();

        var start = startDate.Value;
        var end = endDate.Value;

        var filteredRecords = orders.Where(x => {
            var dt = dateSelector(x);
            return dt >= start && dt <= end;
        });

        if (dataType == PhoneOrderDataDashDataType.Month)
        {
            return filteredRecords
                .GroupBy(x => {
                    var dt = dateSelector(x);
                    return new { dt.Year, dt.Month };
                })
                .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                .ToDictionary(
                    g => $"{g.Key.Year:D4}-{g.Key.Month:D2}",
                    g => g.Count());
        }
        
        return filteredRecords
            .GroupBy(x => dateSelector(x).Date)
            .OrderBy(g => g.Key)
            .ToDictionary(
                g => g.Key.ToString("yyyy-MM-dd"),
                g => g.Count());
    }

    private async Task ApplyPeriodComparisonAsync(GetPhoneOrderDataDashboardRequest request,
        List<PhoneOrderRecord> callInRecords, List<PhoneOrderRecord> callOutRecords, RestaurantDataDto restaurantData,
        CallInDataDto callInData, CallOutDataDto callOutData, CancellationToken cancellationToken)
    {
        var periodDays = (request.EndDate - request.StartDate).TotalDays;
        if (periodDays <= 0) return;

        var prevStartDate = request.StartDate.AddDays(-periodDays);
        var prevEndDate = request.EndDate.AddDays(-periodDays);

        var prevRecords = await _phoneOrderDataProvider.GetPhoneOrderRecordsAsync(
            agentIds: request.AgentIds, null, utcStart: prevStartDate, utcEnd: prevEndDate, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        var prevCallInRecords = prevRecords?.Where(x => x.OrderRecordType == PhoneOrderRecordType.InBound).ToList() ?? new List<PhoneOrderRecord>();
        var prevCallOutRecords = prevRecords?.Where(x => x.OrderRecordType == PhoneOrderRecordType.OutBount).ToList() ?? new List<PhoneOrderRecord>();

        var prevPosOrders = await _posDataProvider.GetPosOrdersByStoreIdsAsync(request.StoreIds, null, true, prevStartDate, prevEndDate, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        var prevCancelledOrders = await _posDataProvider.GetPosOrdersByStoreIdsAsync(
            request.StoreIds, PosOrderModifiedStatus.Cancelled, true, prevStartDate, prevEndDate, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        var prevCallInCount = prevCallInRecords.Count;
        var currCallInCount = callInRecords.Count;
        callInData.CountChange = prevCallInCount == 0 && currCallInCount > 0 ? currCallInCount : currCallInCount - prevCallInCount;

        var prevCallOutCount = prevCallOutRecords.Count;
        var currCallOutCount = callOutRecords.Count;
        callOutData.CountChange = prevCallOutCount == 0 && currCallOutCount > 0 ? currCallOutCount : currCallOutCount - prevCallOutCount;

        var prevOrderCount = prevPosOrders.Count;
        var currOrderCount = restaurantData.OrderCount;
        restaurantData.OrderCountChange = prevOrderCount == 0 && currOrderCount > 0 ? currOrderCount : currOrderCount - prevOrderCount;
        
        var prevOrderAmount = prevPosOrders.Sum(x => x.Total) - prevCancelledOrders.Sum(x => x.Total);
        var currOrderAmount = restaurantData.TotalOrderAmount;
        restaurantData.OrderAmountChange = prevOrderAmount == 0 && currOrderAmount > 0 ? currOrderAmount : currOrderAmount - prevOrderAmount;
    }
    
    private async Task BuildRecordUnreviewDataAsync(List<PhoneOrderRecordDto> records, CancellationToken cancellationToken)
    {
        var recordIds = records.Select(x => x.Id).ToList();
        
        var unreviewedRecordIds = await _posDataProvider.GetAiDraftOrderRecordIdsByRecordIdsAsync(recordIds, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        var reservationRecordIds = records.Where(x => x.Scenario is DialogueScenarios.Reservation or DialogueScenarios.InformationNotification or DialogueScenarios.ThirdPartyOrderNotification).Select(x => x.Id).ToList();
        var unreviewedReservationRecordIds = await _phoneOrderDataProvider.GetPhoneOrderReservationInfoUnreviewedRecordIdsAsync(reservationRecordIds, cancellationToken).ConfigureAwait(false);
        
        Log.Information("Get store unreview record ids: {@UnreviewedRecordIds}", unreviewedRecordIds);
        
        var result = unreviewedReservationRecordIds
            .Union(unreviewedRecordIds)
            .ToList();
        
        records.ForEach(x => x.IsUnreviewed = result.Contains(x.Id));
        
        Log.Information("Enrich complete records: {@Records}", records);
    }
    
    public async Task<GetPhoneOrderRecordScenarioResponse> GetPhoneOrderRecordScenarioAsync(GetPhoneOrderRecordScenarioRequest request, CancellationToken cancellationToken)
    {
        var records = await _phoneOrderDataProvider.GetPhoneOrderRecordScenarioHistoryAsync(request.RecordId, cancellationToken).ConfigureAwait(false);
        
        var result = _mapper.Map<List<PhoneOrderRecordScenarioHistoryDto>>(records);
        
        Log.Information("Get phone order record scenario: {@Result}", result);
        
        return new GetPhoneOrderRecordScenarioResponse
        {
            Data = result
        };
    }
    
    public async Task<GetPhoneOrderRecordTasksResponse> GetPhoneOrderRecordTasksRequestsAsync(
        GetPhoneOrderRecordTasksRequest request, CancellationToken cancellationToken)
    {
        var (utcStart, utcEnd) = ConvertPstDateToUtcRange(request.Date);
        
        var storesAndAgents = await _posDataProvider.GetSimpleStoreAgentsAsync(request.ServiceProviderId, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        var agentIds = storesAndAgents.Select(x => x.AgentId).Distinct().ToList();
        
        if (agentIds.Count == 0) return new GetPhoneOrderRecordTasksResponse();
        
        var events = await _phoneOrderDataProvider.GetWaitingProcessingEventsAsync(agentIds, request.WaitingTaskStatus, utcStart, utcEnd, request.TaskType, cancellationToken).ConfigureAwait(false);

        var (all, unread) = await _phoneOrderDataProvider.GetAllOrUnreadWaitingProcessingEventsAsync(agentIds, cancellationToken).ConfigureAwait(false);
        
        return new GetPhoneOrderRecordTasksResponse
        {
            Data = new GetPhoneOrderRecordTasksDto
            {
                AllCount = all,
                UnreadCount = unread,
                WaitingTasks = events
            }
        };
    }

    public async Task<UpdatePhoneOrderRecordTasksResponse> UpdatePhoneOrderRecordTasksAsync(
        UpdatePhoneOrderRecordTasksCommand command, CancellationToken cancellationToken)
    {
        var waitingProcessingEvents = await _phoneOrderDataProvider.GetWaitingProcessingEventsAsync(ids: command.Ids, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (waitingProcessingEvents.Count < 0) return new UpdatePhoneOrderRecordTasksResponse();

        waitingProcessingEvents = waitingProcessingEvents.Select(x =>
        {
            x.TaskStatus = command.WaitingTaskStatus;
            return x;
        }).ToList();
        
        await _phoneOrderDataProvider.UpdateWaitingProcessingEventsAsync(waitingProcessingEvents, cancellationToken: cancellationToken).ConfigureAwait(false);

        return new UpdatePhoneOrderRecordTasksResponse
        {
            Data = _mapper.Map<List<WaitingProcessingEventsDto>>(waitingProcessingEvents)
        };
    }
}