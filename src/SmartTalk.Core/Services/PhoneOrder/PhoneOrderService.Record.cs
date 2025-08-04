using Serilog;
using System.Text;
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
using SmartTalk.Core.Domain.PhoneOrder;
using SmartTalk.Core.Services.Linphone;
using SmartTalk.Messages.Dto.PhoneOrder;
using SmartTalk.Messages.Dto.Attachments;
using SmartTalk.Messages.Enums.PhoneOrder;
using SmartTalk.Messages.Dto.SpeechMatics;
using SmartTalk.Messages.Enums.SpeechMatics;
using SmartTalk.Messages.Commands.PhoneOrder;
using SmartTalk.Messages.Requests.PhoneOrder;
using SmartTalk.Messages.Commands.Attachments;
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
}

public partial class PhoneOrderService
{
    public async Task<GetPhoneOrderRecordsResponse> GetPhoneOrderRecordsAsync(GetPhoneOrderRecordsRequest request,
        CancellationToken cancellationToken)
    {
        var (utcStart, utcEnd) = ConvertPstDateToUtcRange(request.Date);

        var records = await _phoneOrderDataProvider
            .GetPhoneOrderRecordsAsync(request.AgentId, request.Name, utcStart, utcEnd, cancellationToken)
            .ConfigureAwait(false);

        var enrichedRecords = _mapper.Map<List<PhoneOrderRecordDto>>(records);

        return new GetPhoneOrderRecordsResponse
        {
            Data = enrichedRecords
        };
    }

    public async Task ReceivePhoneOrderRecordAsync(ReceivePhoneOrderRecordCommand command,
        CancellationToken cancellationToken)
    {
        if (command.RecordName.IsNullOrEmpty() && command.RecordUrl.IsNullOrEmpty()) return;

        var recordInfo =
            await ExtractPhoneOrderRecordInfoAsync(command.RecordName, command.AgentId, command.CreatedDate,
                cancellationToken).ConfigureAwait(false);

        Log.Information("Phone order record information: {@recordInfo}", recordInfo);

        if (recordInfo == null) return;
        if (await CheckOrderExistAsync(command.AgentId, recordInfo.StartDate, cancellationToken)
                .ConfigureAwait(false)) return;

        var transcription = await _speechToTextService.SpeechToTextAsync(
                command.RecordContent, fileType: TranscriptionFileType.Wav,
                responseFormat: TranscriptionResponseFormat.Text, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var detection = await _translationClient.DetectLanguageAsync(transcription, cancellationToken)
            .ConfigureAwait(false);

        Log.Information("Phone order record transcription detected language: {@detectionLanguage}", detection.Language);

        var record = new PhoneOrderRecord
        {
            SessionId = Guid.NewGuid().ToString(), AgentId = recordInfo.Agent.Id,
            Language = SelectLanguageEnum(detection.Language), CreatedDate = recordInfo.StartDate,
            Status = PhoneOrderRecordStatus.Recieved
        };

        if (await CheckPhoneOrderRecordDurationAsync(command.RecordContent, cancellationToken).ConfigureAwait(false))
        {
            await AddPhoneOrderRecordAsync(record, PhoneOrderRecordStatus.NoContent, cancellationToken)
                .ConfigureAwait(false);

            return;
        }

        record.Url = command.RecordUrl ??
                     await UploadRecordFileAsync(command.RecordName, command.RecordContent, cancellationToken)
                         .ConfigureAwait(false);

        Log.Information($"Phone order record file url: {record.Url}", record.Url);

        if (string.IsNullOrEmpty(record.Url))
        {
            await AddPhoneOrderRecordAsync(record, PhoneOrderRecordStatus.NoContent, cancellationToken)
                .ConfigureAwait(false);

            return;
        }

        record.TranscriptionJobId = await CreateSpeechMaticsJobAsync(command.RecordContent,
                command.RecordName ?? Guid.NewGuid().ToString("N") + ".wav", detection.Language, cancellationToken)
            .ConfigureAwait(false);

        await AddPhoneOrderRecordAsync(record, PhoneOrderRecordStatus.Diarization, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<bool> CheckOrderExistAsync(int agentId, DateTimeOffset createdDate,
        CancellationToken cancellationToken)
    {
        return (await _phoneOrderDataProvider
            .GetPhoneOrderRecordsAsync(agentId: agentId, createdDate: createdDate, cancellationToken: cancellationToken)
            .ConfigureAwait(false)).Any();
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
        List<SpeechMaticsSpeakInfoDto> phoneOrderInfo, PhoneOrderRecord record, byte[] audioContent,
        CancellationToken cancellationToken)
    {
        if (phoneOrderInfo is { Count: 0 }) return;

        try
        {
            phoneOrderInfo =
                await HandlerConversationFirstSentenceAsync(phoneOrderInfo, record, audioContent, cancellationToken)
                    .ConfigureAwait(false);

            Log.Information("Phone order record info: {@phoneOrderInfo}", phoneOrderInfo);

            var (goalText, tip) =
                await PhoneOrderTranscriptionAsync(phoneOrderInfo, record, audioContent, cancellationToken)
                    .ConfigureAwait(false);

            await _phoneOrderUtilService.ExtractPhoneOrderShoppingCartAsync(goalText, record, cancellationToken)
                .ConfigureAwait(false);

            record.Tips = tip;
        }
        catch (Exception e)
        {
            Log.Error("Extract phone order record error: {@Error}", e);
        }
    }

    public async Task<AddOrUpdateManualOrderResponse> AddOrUpdateManualOrderAsync(AddOrUpdateManualOrderCommand command,
        CancellationToken cancellationToken)
    {
        var orderId = long.Parse(command.OrderId);

        Log.Information($"Add manual order: {orderId}", orderId);

        var manualOrder = await _easyPosClient.GetOrderAsync(orderId, command.Restaurant, cancellationToken)
            .ConfigureAwait(false);

        Log.Information("Get order response: response: {@manualOrder}", manualOrder);

        if (manualOrder.Data == null)
            return new AddOrUpdateManualOrderResponse
            {
                Msg = "pos not find order"
            };

        var items = await _phoneOrderDataProvider
            .GetPhoneOrderOrderItemsAsync(command.RecordId, PhoneOrderOrderType.ManualOrder, cancellationToken)
            .ConfigureAwait(false);

        if (items is { Count: > 0 })
            await _phoneOrderDataProvider.DeletePhoneOrderItemsAsync(items, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

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

        var record =
            (await _phoneOrderDataProvider
                .GetPhoneOrderRecordAsync(command.RecordId, cancellationToken: cancellationToken).ConfigureAwait(false))
            .FirstOrDefault();

        record.ManualOrderId = orderId;

        await _phoneOrderDataProvider.UpdatePhoneOrderRecordsAsync(record, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        await _phoneOrderDataProvider.AddPhoneOrderItemAsync(oderItems, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

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
                note = note +
                       $"{condiment.ActionLocalizations.First(c => c.Field == "name" && c.languageCode == "zh_CN").Value + condiment.Localizations.First(c => c.Field == "name" && c.languageCode == "zh_CN").Value} (${condiment.Price})";
            else
                note = note + $"{condiment.Notes}(${condiment.Price})";
        }

        return note;
    }

    private async Task<List<SpeechMaticsSpeakInfoDto>> HandlerConversationFirstSentenceAsync(
        List<SpeechMaticsSpeakInfoDto> phoneOrderInfos, PhoneOrderRecord record, byte[] audioContent,
        CancellationToken cancellationToken)
    {
        var originText = await SplitAudioAsync(audioContent, record, phoneOrderInfos[0].StartTime * 1000,
            phoneOrderInfos[0].EndTime * 1000,
            TranscriptionFileType.Wav, cancellationToken).ConfigureAwait(false);

        if (await CheckAudioFirstSentenceIsRestaurantAsync(originText, cancellationToken).ConfigureAwait(false))
            return phoneOrderInfos;

        foreach (var phoneOrderInfo in phoneOrderInfos)
        {
            phoneOrderInfo.Speaker = phoneOrderInfo.Speaker == "S1" ? "S2" : "S1";
            phoneOrderInfo.Role = phoneOrderInfo.Role == PhoneOrderRole.Restaurant
                ? PhoneOrderRole.Client
                : PhoneOrderRole.Restaurant;
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
        List<SpeechMaticsSpeakInfoDto> phoneOrderInfo, PhoneOrderRecord record, byte[] audioContent,
        CancellationToken cancellationToken)
    {
        var conversationIndex = 0;
        var goalTexts = new List<string>();
        var conversations = new List<PhoneOrderConversation>();

        foreach (var speakDetail in phoneOrderInfo)
        {
            Log.Information(
                "Start time of speak in video: {SpeakStartTimeVideo}, End time of speak in video: {SpeakEndTimeVideo}",
                speakDetail.StartTime * 1000, speakDetail.EndTime * 1000);

            try
            {
                string originText;

                if (speakDetail.StartTime != 0 && speakDetail.EndTime != 0)
                    originText = await SplitAudioAsync(
                        audioContent, record, speakDetail.StartTime * 1000, speakDetail.EndTime * 1000,
                        TranscriptionFileType.Wav, cancellationToken).ConfigureAwait(false);
                else
                    originText = "";

                Log.Information("Phone Order transcript originText: {originText}", originText);

                goalTexts.Add((speakDetail.Role == PhoneOrderRole.Restaurant
                    ? PhoneOrderRole.Restaurant.ToString()
                    : PhoneOrderRole.Client.ToString()) + ": " + originText);

                if (speakDetail.Role == PhoneOrderRole.Restaurant)
                    conversations.Add(new PhoneOrderConversation
                    {
                        RecordId = record.Id, Question = originText, Order = conversationIndex,
                        StartTime = speakDetail.StartTime, EndTime = speakDetail.EndTime
                    });
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

        await _phoneOrderDataProvider.AddPhoneOrderConversationsAsync(conversations.Count != 0
            ? conversations
            :
            [
                new PhoneOrderConversation
                    { Question = goalTextsString, Answer = string.Empty, RecordId = record.Id, Order = 0 }
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

                goalTextsString = goalTextsString + "Restaurant: " + conversation.Question + "\nClient:" +
                                  conversation.Answer + "\n";
            }
        }

        Log.Information("Processed conversation:{@conversations}， goalText:{@goalTextsString}", conversations,
            goalTextsString);

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

    private async Task AddPhoneOrderRecordAsync(PhoneOrderRecord record, PhoneOrderRecordStatus status,
        CancellationToken cancellationToken)
    {
        record.Status = status;

        await _phoneOrderDataProvider
            .AddPhoneOrderRecordsAsync(new List<PhoneOrderRecord> { record }, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<bool> CheckPhoneOrderRecordDurationAsync(byte[] recordContent,
        CancellationToken cancellationToken)
    {
        var audioDuration = await _ffmpegService.GetAudioDurationAsync(recordContent, cancellationToken)
            .ConfigureAwait(false);

        Log.Information($"Phone order record audio duration: {audioDuration}", audioDuration);

        var timeSpan = TimeSpan.Parse(audioDuration);

        return timeSpan.TotalSeconds < 15 &&
               (timeSpan.TotalSeconds < 3 || timeSpan.Seconds == 14 || timeSpan.Seconds == 10);
    }

    private async Task<string> UploadRecordFileAsync(string fileName, byte[] fileContent,
        CancellationToken cancellationToken)
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

    private async Task<PhoneOrderRecordInformationDto> ExtractPhoneOrderRecordInfoAsync(string recordName, int agentId,
        DateTimeOffset? startTime, CancellationToken cancellationToken)
    {
        var agent = await _agentDataProvider.GetAgentByIdAsync(agentId, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

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

        return TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeSeconds(long.Parse(time)),
            TimeZoneInfo.FindSystemTimeZoneById("America/Los_Angeles"));
    }

    private async Task UpdatePhoneOrderRecordSpecificFieldsAsync(int recordId, int modifiedBy, string tips,
        string lastModifiedByName, CancellationToken cancellationToken)
    {
        var record =
            (await _phoneOrderDataProvider.GetPhoneOrderRecordAsync(recordId, cancellationToken: cancellationToken)
                .ConfigureAwait(false)).FirstOrDefault();

        if (record == null) return;

        record.Tips = tips;
        record.LastModifiedBy = modifiedBy;
        record.LastModifiedDate = DateTimeOffset.Now;
        record.LastModifiedByName = lastModifiedByName;

        await _phoneOrderDataProvider.UpdatePhoneOrderRecordsAsync(record, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<string> SplitAudioAsync(byte[] file, PhoneOrderRecord record, double speakStartTimeVideo,
        double speakEndTimeVideo, TranscriptionFileType fileType = TranscriptionFileType.Wav,
        CancellationToken cancellationToken = default)
    {
        if (file == null) return null;

        var audioBytes = await _ffmpegService.ConvertFileFormatAsync(file, fileType, cancellationToken)
            .ConfigureAwait(false);

        var splitAudios = await _ffmpegService
            .SpiltAudioAsync(audioBytes, speakStartTimeVideo, speakEndTimeVideo, cancellationToken)
            .ConfigureAwait(false);

        var transcriptionResult = new StringBuilder();

        foreach (var reSplitAudio in splitAudios)
        {
            var transcriptionResponse = await _speechToTextService.SpeechToTextAsync(
                    reSplitAudio, record.Language, TranscriptionFileType.Wav, TranscriptionResponseFormat.Text,
                    record.RestaurantInfo?.Message ?? string.Empty, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            transcriptionResult.Append(transcriptionResponse);
        }

        Log.Information("Transcription result {Transcription}", transcriptionResult.ToString());

        return transcriptionResult.ToString();
    }

    private async Task<string> CreateTranscriptionJobAsync(byte[] data, string fileName, string language,
        CancellationToken cancellationToken)
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

        return await _speechMaticsClient
            .CreateJobAsync(new SpeechMaticsCreateJobRequestDto { JobConfig = jobConfigDto }, createTranscriptionDto,
                cancellationToken).ConfigureAwait(false);
    }

    private SpeechMaticsLanguageType SelectSpeechMetisLanguageType(string language)
    {
        return language switch
        {
            "en" => SpeechMaticsLanguageType.En,
            "zh" => SpeechMaticsLanguageType.Yue,
            "zh-CN" or "zh-TW" => SpeechMaticsLanguageType.Cmn,
            _ => SpeechMaticsLanguageType.En
        };
    }

    public async Task<string> CreateSpeechMaticsJobAsync(byte[] recordContent, string recordName, string language,
        CancellationToken cancellationToken)
    {
        var retryCount = 2;

        while (true)
        {
            var transcriptionJobIdJObject =
                JObject.Parse(await CreateTranscriptionJobAsync(recordContent, recordName, language, cancellationToken)
                    .ConfigureAwait(false));

            var transcriptionJobId = transcriptionJobIdJObject["id"]?.ToString();

            Log.Information("Phone order record transcriptionJobId: {@transcriptionJobId}", transcriptionJobId);

            if (transcriptionJobId != null)
                return transcriptionJobId;

            Log.Information("Create speechMatics job abnormal, start replacement key");

            var keys = await _speechMaticsDataProvider.GetSpeechMaticsKeysAsync(
                    [SpeechMaticsKeyStatus.Active, SpeechMaticsKeyStatus.NotEnabled],
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

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

            await _speechMaticsDataProvider
                .UpdateSpeechMaticsKeysAsync([notEnabledKey, activeKey], cancellationToken: cancellationToken)
                .ConfigureAwait(false);

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
            .GetPhonCallUsagesAsync(startTime, endTime, request.IncludeExternalData, cancellationToken)
            .ConfigureAwait(false);

        var data = result.GroupBy(x => x.Record.AgentId).Select(x => new PhoneCallRecordDetailDto
        {
            Name = x.First().Assistant?.Name,
            Records = _mapper.Map<List<PhoneOrderRecordDto>>(x.Select(r => r.Record))
        }).ToList();
        
        var fileUrl = await GenerateExcelFileAsync(data, cancellationToken).ConfigureAwait(false);

        return new GetPhoneCallRecordDetailResponse { Data = fileUrl };
    }

    private (DateTimeOffset Start, DateTimeOffset End) GetQueryTimeRange(int month)
    {
        var pacificZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
        
        var startLocal = new DateTime(2025, month, 1, 0, 0, 0);
        var endLocal = new DateTime(2025, month, 31, 23, 59, 59);
        
        var startInPst = new DateTimeOffset(startLocal, pacificZone.GetUtcOffset(startLocal));
        var endInPst = new DateTimeOffset(endLocal, pacificZone.GetUtcOffset(endLocal));
        
        return (startInPst.ToUniversalTime(), endInPst.ToUniversalTime());
    }
    
    private async Task<string> GenerateExcelFileAsync(List<PhoneCallRecordDetailDto> details, CancellationToken cancellationToken)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Sheet1");
        var currentRow = 1;

        foreach (var detail in details)
        {
            // 1️⃣ 報表標題
            worksheet.Cell(currentRow, 1).Value = $"{detail.Name}";
            worksheet.Range(currentRow, 1, currentRow, 3).Merge();
            var titleCell = worksheet.Cell(currentRow, 1);
            titleCell.Style.Font.Bold = true;
            titleCell.Style.Font.FontSize = 14;
            titleCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            titleCell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            worksheet.Row(currentRow).Height = 20;

            currentRow++;

            // 2️⃣ 欄位列
            worksheet.Cell(currentRow, 1).Value = "錄音";
            worksheet.Cell(currentRow, 2).Value = "時長";
            worksheet.Cell(currentRow, 3).Value = "是否轉接人工";

            var headerRange = worksheet.Range(currentRow, 1, currentRow, 3);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
            headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            headerRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            currentRow++;

            // 3️⃣ 數據列（多筆）
            foreach (var record in detail.Records)
            {
                worksheet.Cell(currentRow, 1).Value = record.Url;
                worksheet.Cell(currentRow, 2).Value = record.Duration;
                worksheet.Cell(currentRow, 3).Value = record.IsTransfer.HasValue ? record.IsTransfer.Value ? "Yes" : "No" : "No";

                var dataRange = worksheet.Range(currentRow, 1, currentRow, 3);
                dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

                currentRow++;
            }

            // 4️⃣ 空行作為分隔
            currentRow++;
        }

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        
        var audio = await _attachmentService.UploadAttachmentAsync(new UploadAttachmentCommand
        {
            Attachment = new UploadAttachmentDto { FileName = Guid.NewGuid() + ".wav", FileContent = stream.ToArray() }
        }, cancellationToken).ConfigureAwait(false);

        return audio.Attachment?.FileUrl ?? string.Empty;
    }
}