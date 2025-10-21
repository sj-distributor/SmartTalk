using System.Reflection;
using Serilog;
using System.Text;
using System.Text.Json.Serialization;
using Newtonsoft.Json.Linq;
using SmartTalk.Messages.Enums.STT;
using Smarties.Messages.DTO.OpenAi;
using SmartTalk.Messages.Dto.Agent;
using SmartTalk.Messages.Dto.EasyPos;
using SmartTalk.Messages.Dto.WeChat;
using Microsoft.IdentityModel.Tokens;
using Smarties.Messages.Enums.OpenAi;
using Smarties.Messages.Requests.Ask;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Newtonsoft.Json;
using SmartTalk.Core.Domain.PhoneOrder;
using SmartTalk.Core.Domain.Pos;
using SmartTalk.Core.Services.Linphone;
using SmartTalk.Messages.Dto.PhoneOrder;
using SmartTalk.Messages.Dto.Attachments;
using SmartTalk.Messages.Enums.PhoneOrder;
using SmartTalk.Messages.Dto.SpeechMatics;
using SmartTalk.Messages.Enums.SpeechMatics;
using SmartTalk.Messages.Commands.PhoneOrder;
using SmartTalk.Messages.Requests.PhoneOrder;
using SmartTalk.Messages.Commands.Attachments;
using SmartTalk.Messages.Enums.Account;
using SmartTalk.Messages.Enums.Pos;
using TranscriptionFileType = SmartTalk.Messages.Enums.STT.TranscriptionFileType;
using TranscriptionResponseFormat = SmartTalk.Messages.Enums.STT.TranscriptionResponseFormat;

namespace SmartTalk.Core.Services.PhoneOrder;

public partial interface IPhoneOrderService
{
    Task<GetPhoneOrderRecordsResponse> GetPhoneOrderRecordsAsync(GetPhoneOrderRecordsRequest request, CancellationToken cancellationToken);

    Task ReceivePhoneOrderRecordAsync(ReceivePhoneOrderRecordCommand command, CancellationToken cancellationToken);

    Task ExtractPhoneOrderRecordAiMenuAsync(List<SpeechMaticsSpeakInfoDto> phoneOrderInfo, PhoneOrderRecord record, byte[] audioContent, CancellationToken cancellationToken);

    Task<AddOrUpdateManualOrderResponse> AddOrUpdateManualOrderAsync(AddOrUpdateManualOrderCommand command, CancellationToken cancellationToken);

    Task<string> CreateSpeechMaticsJobAsync(byte[] recordContent, string recordName, string language, CancellationToken cancellationToken);
    
    Task<GetPhoneCallUsagesPreviewResponse> GetPhoneCallUsagesPreviewAsync(GetPhoneCallUsagesPreviewRequest request, CancellationToken cancellationToken);

    Task<GetPhoneCallRecordDetailResponse> GetPhoneCallrecordDetailAsync(GetPhoneCallRecordDetailRequest request, CancellationToken cancellationToken);

    Task<GetPhoneOrderRecordReportResponse> GetPhoneOrderRecordReportByCallSidAsync(GetPhoneOrderRecordReportRequest request, CancellationToken cancellationToken);
    
    Task<GetPhoneOrderDataDashboardResponse> GetPhoneOrderDataDashboardAsync(GetPhoneOrderDataDashboardRequest request, CancellationToken cancellationToken);
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

        var records = await _phoneOrderDataProvider.GetPhoneOrderRecordsAsync(agentIds, request.Name, request.OrderId, utcStart, utcEnd, cancellationToken).ConfigureAwait(false);

        var enrichedRecords = _mapper.Map<List<PhoneOrderRecordDto>>(records);

        return new GetPhoneOrderRecordsResponse
        {
            Data = enrichedRecords
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
            Status = PhoneOrderRecordStatus.Recieved
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

        record.TranscriptionJobId = await CreateSpeechMaticsJobAsync(command.RecordContent, command.RecordName ?? Guid.NewGuid().ToString("N") + ".wav", detection.Language, cancellationToken).ConfigureAwait(false);

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

            record.ConversationText = goalText;

            await _phoneOrderUtilService.ExtractPhoneOrderShoppingCartAsync(goalText, record, cancellationToken).ConfigureAwait(false);

            record.Tips = tip;
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

        return new PhoneOrderRecordInformationDto
        {
            Agent = _mapper.Map<AgentDto>(agent),
            StartDate = startTime ?? ExtractPhoneOrderStartDateFromRecordName(recordName)
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

        var splitAudios = await _ffmpegService.SpiltAudioAsync(audioBytes, speakStartTimeVideo, speakEndTimeVideo, cancellationToken).ConfigureAwait(false);

        var transcriptionResult = new StringBuilder();

        foreach (var reSplitAudio in splitAudios)
        {
            try
            {
                var transcriptionResponse = await _speechToTextService.SpeechToTextAsync(
                    reSplitAudio, record.Language, TranscriptionFileType.Wav, TranscriptionResponseFormat.Text,
                    record.RestaurantInfo?.Message ?? string.Empty, cancellationToken: cancellationToken).ConfigureAwait(false);

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

    private async Task<string> CreateTranscriptionJobAsync(byte[] data, string fileName, string language, CancellationToken cancellationToken)
    {
        var createTranscriptionDto = new SpeechMaticsCreateTranscriptionDto { Data = data, FileName = fileName };

        var jobConfigDto = new SpeechMaticsJobConfigDto
        {
            Type = SpeechMaticsJobType.Transcription,
            TranscriptionConfig = new SpeechMaticsTranscriptionConfigDto
            {
                Language = SelectSpeechMetisLanguageType(language),
                Diarization = SpeechMaticsDiarizationType.Speaker,
                OperatingPoint = SpeechMaticsOperatingPointType.Enhanced
            },
            NotificationConfig = new List<SpeechMaticsNotificationConfigDto>
            {
                new SpeechMaticsNotificationConfigDto
                {
                    AuthHeaders = _transcriptionCallbackSetting.AuthHeaders,
                    Contents = new List<string> { "transcript" },
                    Url = _transcriptionCallbackSetting.Url
                }
            }
        };

        return await _speechMaticsClient.CreateJobAsync(new SpeechMaticsCreateJobRequestDto { JobConfig = jobConfigDto }, createTranscriptionDto, cancellationToken).ConfigureAwait(false);
    }

    private SpeechMaticsLanguageType SelectSpeechMetisLanguageType(string language)
    {
        return language switch
        {
            "en" => SpeechMaticsLanguageType.En,
            "zh" => SpeechMaticsLanguageType.Yue,
            "zh-CN" or "zh-TW" => SpeechMaticsLanguageType.Cmn,
            "es" => SpeechMaticsLanguageType.Es,
            "ko" => SpeechMaticsLanguageType.Ko,
            _ => SpeechMaticsLanguageType.En
        };
    }

    public async Task<string> CreateSpeechMaticsJobAsync(byte[] recordContent, string recordName, string language,
        CancellationToken cancellationToken)
    {
        var retryCount = 2;

        while (true)
        {
            var transcriptionJobIdJObject = JObject.Parse(await CreateTranscriptionJobAsync(recordContent, recordName, language, cancellationToken).ConfigureAwait(false));

            var transcriptionJobId = transcriptionJobIdJObject["id"]?.ToString();

            Log.Information("Phone order record transcriptionJobId: {@transcriptionJobId}", transcriptionJobId);

            if (transcriptionJobId != null)
                return transcriptionJobId;

            Log.Information("Create speechMatics job abnormal, start replacement key");

            var keys = await _speechMaticsDataProvider.GetSpeechMaticsKeysAsync(
                    [SpeechMaticsKeyStatus.Active, SpeechMaticsKeyStatus.NotEnabled], cancellationToken: cancellationToken).ConfigureAwait(false);

            Log.Information("Get speechMatics keys：{@keys}", keys);

            var activeKey = keys.FirstOrDefault(x => x.Status == SpeechMaticsKeyStatus.Active);

            var notEnabledKey = keys.FirstOrDefault(x => x.Status == SpeechMaticsKeyStatus.NotEnabled);

            if (notEnabledKey != null && activeKey != null)
            {
                notEnabledKey.Status = SpeechMaticsKeyStatus.Active;
                notEnabledKey.LastModifiedDate = DateTimeOffset.Now;
                activeKey.Status = SpeechMaticsKeyStatus.Discard;
            }

            Log.Information("Update speechMatics keys：{@keys}", keys);

            await _speechMaticsDataProvider.UpdateSpeechMaticsKeysAsync([notEnabledKey, activeKey], cancellationToken: cancellationToken).ConfigureAwait(false);

            retryCount--;

            if (retryCount <= 0)
            {
                await _weChatClient.SendWorkWechatRobotMessagesAsync(
                    _speechMaticsKeySetting.SpeechMaticsKeyEarlyWarningRobotUrl,
                    new SendWorkWechatGroupRobotMessageDto
                    {
                        MsgType = "text",
                        Text = new SendWorkWechatGroupRobotTextDto
                        {
                            Content = $"SMT Speech Matics Key Error"
                        }
                    }, cancellationToken).ConfigureAwait(false);

                return null;
            }

            Log.Information("Retrying Create Speech Matics Job Attempts remaining: {RetryCount}", retryCount);
        }
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
    
    public async Task<GetPhoneOrderDataDashboardResponse> GetPhoneOrderDataDashboardAsync(GetPhoneOrderDataDashboardRequest request, CancellationToken cancellationToken)
    {
        var unixStart = request.StartDate.ToUnixTimeSeconds();
        var unixEnd = request.EndDate.ToUnixTimeSeconds();

        var records = await _phoneOrderDataProvider.GetPhoneOrderRecordsAsync(agentIds: request.AgentIds, null, utcStart: request.StartDate, utcEnd: request.EndDate, cancellationToken: cancellationToken).ConfigureAwait(false);

        var linphoneSips = await _linphoneDataProvider.GetLinphoneSipsByAgentIdsAsync(agentIds: request.AgentIds, cancellationToken: cancellationToken).ConfigureAwait(false);
        var sipNumbers = linphoneSips.Select(y => y.Sip).ToList();
        
        var (callInFailedCount, callOutFailedCount) = await _linphoneDataProvider.GetCallFailedStatisticsAsync(unixStart, unixEnd, sipNumbers, cancellationToken).ConfigureAwait(false);
        
        if (records == null || records.Count == 0) 
        { return new GetPhoneOrderDataDashboardResponse { Data = new GetPhoneOrderDataDashboardResponseData() }; }
        
        var posOrders = await _posDataProvider.GetPosOrdersByStoreIdsAsync(request.StoreIds, null, true, request.StartDate, request.EndDate, cancellationToken: cancellationToken).ConfigureAwait(false);
        var cancelledOrders = await _posDataProvider.GetPosOrdersByStoreIdsAsync(request.StoreIds, PosOrderModifiedStatus.Cancelled, true, request.StartDate, request.EndDate, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        var phoneOrderReports = await _phoneOrderDataProvider.GetPhoneOrderRecordReportByRecordIdAsync(recordId: records.Select(x => x.Id).ToList(), cancellationToken: cancellationToken).ConfigureAwait(false);
        
        var callInRecords = records.Where(x => !x.IsOutBount).ToList();
        var callOutRecords = records.Where(x => x.IsOutBount).ToList();
        
        var callInData = BuildCallInData(callInRecords, callInFailedCount, phoneOrderReports, request.InvalidCallSeconds, request.StartDate, request.EndDate, request.DataType);
        var callOutData = BuildCallOutData(callOutRecords, callOutFailedCount, phoneOrderReports, request.InvalidCallSeconds, request.StartDate, request.EndDate, request.DataType);
        
        var orderCountPerPeriod = GroupCountByRequestType(posOrders, x => x.CreatedDate, request.StartDate, request.EndDate, request.DataType);
        var cancelledOrderCountPerPeriod = GroupCountByRequestType(cancelledOrders, x => x.CreatedDate, request.StartDate, request.EndDate, request.DataType);
        
        var restaurantData = new RestaurantDataDto
        {
            OrderCount = posOrders.Count,
            TotalOrderAmount = posOrders.Sum(x => x.Total) - cancelledOrders.Sum(x => x.Total),
            CancelledOrderCount = cancelledOrders.Count,
            OrderCountPerPeriod = orderCountPerPeriod,
            CancelledOrderCountPerPeriod = cancelledOrderCountPerPeriod
        };
        
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

    private static CallInDataDto BuildCallInData(List<PhoneOrderRecord> callInRecords, int callInFailedCount, List<PhoneOrderRecordReport> phoneOrderReports, int? invalidCallSeconds, DateTimeOffset? start, DateTimeOffset? end, PhoneOrderDataDashDataType dataType)
    {
        var answeredCount = callInRecords.Count;

        var totalRepeatCalls = callInRecords.GroupBy(x => x.PhoneNumber).Select(g => Math.Max(0, g.Count() - 1)).Sum();

        var effectiveCount = answeredCount - callInRecords.Count(x => (x.Duration ?? 0) <= invalidCallSeconds);
        var averageDuration = callInRecords.DefaultIfEmpty().Average(x => x?.Duration ?? 0);
        var totalDuration = callInRecords.Sum(x => x.Duration ?? 0);
        var friendlyCount = phoneOrderReports.Count(x => x.IsCustomerFriendly);
        var satisfactionRate = answeredCount > 0 ? (double)friendlyCount / answeredCount : 0;
        var transferCount = callInRecords.Count(x => x.IsTransfer ?? false);
        var transferRate = answeredCount > 0 ? (double)transferCount / answeredCount : 0;
        var repeatRate = answeredCount > 0 ? (double)totalRepeatCalls / answeredCount : 0;

        var totalDurationPerPeriod = GroupDurationByRequestType(callInRecords, start, end, dataType);

        return new CallInDataDto
        {
            AnsweredCallInCount = answeredCount,
            AverageCallInDurationSeconds = averageDuration,
            EffectiveCommunicationCallInCount = effectiveCount,
            RepeatCallInRate = repeatRate,
            CallInSatisfactionRate = satisfactionRate,
            CallInMissedByHumanCount = callInFailedCount,
            CallinTransferToHumanRate = transferRate,
            TotalCallInDurationSeconds = totalDuration,
            TotalCallInDurationPerPeriod = totalDurationPerPeriod
        };
    }

    private static CallOutDataDto BuildCallOutData(List<PhoneOrderRecord> callOutRecords, int callInFailedCount, List<PhoneOrderRecordReport> phoneOrderReports, int? invalidCallSeconds, DateTimeOffset? start, DateTimeOffset? end, PhoneOrderDataDashDataType dataType)
    {
        var answeredCount = callOutRecords.Count;
        var effectiveCount = answeredCount - callOutRecords.Count(x => (x.Duration ?? 0) <= invalidCallSeconds);
        var averageDuration = callOutRecords.DefaultIfEmpty().Average(x => x?.Duration ?? 0);
        var totalDuration = callOutRecords.Sum(x => x.Duration ?? 0);
        var friendlyCount = phoneOrderReports.Count(x => x.IsCustomerFriendly);
        var satisfactionRate = answeredCount > 0 ? (double)friendlyCount / answeredCount : 0;
        var transferCount = callOutRecords.Count(x => x.IsTransfer ?? false);

        var totalDurationPerPeriod = GroupDurationByRequestType(callOutRecords, start, end, dataType);

        return new CallOutDataDto
        {
            AnsweredCallOutCount = answeredCount,
            AverageCallOutDurationSeconds = averageDuration,
            EffectiveCommunicationCallOutCount = effectiveCount,
            CallOutNotAnsweredCount = callInFailedCount,
            CallOutAnsweredByHumanCount = transferCount,
            CallOutSatisfactionRate = satisfactionRate,
            TotalCallOutDurationSeconds = totalDuration,
            TotalCallOutDurationPerPeriod = totalDurationPerPeriod
        };
    }
    
    private static Dictionary<string, double> GroupDurationByRequestType(List<PhoneOrderRecord> records, DateTimeOffset? startDate, DateTimeOffset? endDate, PhoneOrderDataDashDataType dataType)
    {
        if (startDate == null || endDate == null) return new Dictionary<string, double>();

        var start = startDate.Value;
        var end = endDate.Value;

        var filteredRecords = records.Where(x => x.CreatedDate >= start && x.CreatedDate <= end);

        if (dataType == PhoneOrderDataDashDataType.Month)
        {
            return filteredRecords
                .GroupBy(x => new { x.CreatedDate.Year, x.CreatedDate.Month })
                .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                .ToDictionary(
                    g => $"{g.Key.Year:D4}-{g.Key.Month:D2}",
                    g => g.Sum(x => x.Duration ?? 0));
        }
        
        return filteredRecords
            .GroupBy(x => x.CreatedDate.Date)
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
        var prevEndDate = request.StartDate;

        var prevRecords = await _phoneOrderDataProvider.GetPhoneOrderRecordsAsync(
            agentIds: request.AgentIds, null, utcStart: prevStartDate, utcEnd: prevEndDate, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        var prevCallInRecords = prevRecords?.Where(x => !x.IsOutBount).ToList() ?? new List<PhoneOrderRecord>();
        var prevCallOutRecords = prevRecords?.Where(x => x.IsOutBount).ToList() ?? new List<PhoneOrderRecord>();

        var prevPosOrders = await _posDataProvider.GetPosOrdersByStoreIdsAsync(request.StoreIds, null, true, prevStartDate, prevEndDate, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        var prevCancelledOrders = await _posDataProvider.GetPosOrdersByStoreIdsAsync(
            request.StoreIds, PosOrderModifiedStatus.Cancelled, true, prevStartDate, prevEndDate, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        callInData.CountChange = callInRecords.Count - prevCallInRecords.Count;
        callOutData.CountChange = callOutRecords.Count - prevCallOutRecords.Count;

        restaurantData.OrderCountChange = restaurantData.OrderCount - prevPosOrders.Count;
        restaurantData.OrderAmountChange = restaurantData.TotalOrderAmount - (prevPosOrders.Sum(x => x.Total) - prevCancelledOrders.Sum(x => x.Total));
    }
}