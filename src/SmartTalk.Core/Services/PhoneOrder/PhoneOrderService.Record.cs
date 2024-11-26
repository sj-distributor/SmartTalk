using System.Net;
using Serilog;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SmartTalk.Messages.Enums.STT;
using Smarties.Messages.DTO.OpenAi;
using Microsoft.IdentityModel.Tokens;
using Smarties.Messages.Enums.OpenAi;
using Smarties.Messages.Requests.Ask;
using System.Text.RegularExpressions;
using SmartTalk.Core.Domain.PhoneOrder;
using SmartTalk.Core.Domain.SpeechMatics;
using SmartTalk.Core.Extensions;
using SmartTalk.Messages.Dto.WebSocket;
using SmartTalk.Messages.Dto.PhoneOrder;
using SmartTalk.Messages.Dto.Attachments;
using SmartTalk.Messages.Enums.PhoneOrder;
using SmartTalk.Messages.Dto.SpeechMatics;
using SmartTalk.Messages.Enums.SpeechMatics;
using SmartTalk.Messages.Commands.PhoneOrder;
using SmartTalk.Messages.Requests.PhoneOrder;
using SmartTalk.Messages.Commands.Attachments;
using SmartTalk.Messages.Constants;
using SmartTalk.Messages.Dto.EasyPos;
using SmartTalk.Messages.Dto.Restaurant;
using SmartTalk.Messages.Dto.WeChat;
using TranscriptionFileType = SmartTalk.Messages.Enums.STT.TranscriptionFileType;
using TranscriptionResponseFormat = SmartTalk.Messages.Enums.STT.TranscriptionResponseFormat;

namespace SmartTalk.Core.Services.PhoneOrder;

public partial interface IPhoneOrderService
{
    Task<GetPhoneOrderRecordsResponse> GetPhoneOrderRecordsAsync(GetPhoneOrderRecordsRequest request, CancellationToken cancellationToken);

    Task ReceivePhoneOrderRecordAsync(ReceivePhoneOrderRecordCommand command, CancellationToken cancellationToken);

    Task ExtractPhoneOrderRecordAiMenuAsync(List<SpeechMaticsSpeakInfoDto> phoneOrderInfo, PhoneOrderRecord record, byte[] audioContent, CancellationToken cancellationToken);

    Task<AddOrUpdateManualOrderResponse> AddOrUpdateManualOrderAsync(AddOrUpdateManualOrderCommand command, CancellationToken cancellationToken);
}

public partial class PhoneOrderService
{
    public async Task<GetPhoneOrderRecordsResponse> GetPhoneOrderRecordsAsync(GetPhoneOrderRecordsRequest request, CancellationToken cancellationToken)
    {
        var records = await _phoneOrderDataProvider.GetPhoneOrderRecordsAsync(request.Restaurant, cancellationToken).ConfigureAwait(false);

        return new GetPhoneOrderRecordsResponse
        {
            Data = _mapper.Map<List<PhoneOrderRecordDto>>(records)
        };
    }

    public async Task ReceivePhoneOrderRecordAsync(ReceivePhoneOrderRecordCommand command, CancellationToken cancellationToken)
    {
        if (command.RecordName.IsNullOrEmpty()) return;
        
        var recordInfo = ExtractPhoneOrderRecordInfoFromRecordName(command.RecordName);
        
        Log.Information("Phone order record information: {@recordInfo}", recordInfo);

        if (await CheckOrderExistAsync(recordInfo.OrderDate.AddHours(-8), cancellationToken).ConfigureAwait(false)) return;
        
        var transcription = await _speechToTextService.SpeechToTextAsync(
            command.RecordContent, fileType: TranscriptionFileType.Wav, responseFormat: TranscriptionResponseFormat.Text, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrEmpty(transcription) || transcription.Length < 15 || transcription.Contains("GENERAL") || transcription.Contains("感謝收看") || transcription.Contains("訂閱") || transcription.Contains("点赞") || transcription.Contains("立場") || transcription.Contains("字幕") || transcription.Contains("結束") || transcription.Contains("謝謝觀看") || transcription.Contains("幕後大臣") || transcription == "醒醒" || transcription == "跟著我" || transcription.Contains("政經關峻") || transcription.Contains("您拨打的电话") || transcription.Contains("Mailbox memory is full") || transcription.Contains("amazon", StringComparison.InvariantCultureIgnoreCase) || transcription.Contains("We're sorry, your call did not go through.", StringComparison.InvariantCultureIgnoreCase) || transcription.Contains("Verizon Wireless", StringComparison.InvariantCultureIgnoreCase) || transcription.Contains("Beep", StringComparison.InvariantCultureIgnoreCase) || transcription.Contains("USPS customer service center", StringComparison.InvariantCultureIgnoreCase) || transcription.Contains("not go through", StringComparison.InvariantCultureIgnoreCase) || transcription.Contains("stop serving in two hours", StringComparison.InvariantCultureIgnoreCase) || transcription.Contains("Welcome to customer support", StringComparison.InvariantCultureIgnoreCase) || transcription.Contains("For the upcoming flight booking", StringComparison.InvariantCultureIgnoreCase) || transcription.Contains("Please check and dial again.", StringComparison.InvariantCultureIgnoreCase) || transcription.Contains("largest cable TV network", StringComparison.InvariantCultureIgnoreCase) || transcription.Contains("拨打的用户暂时无法接通") || transcription.Contains("您有一份国际快递即将退回")) return;
        
        var detection = await _translationClient.DetectLanguageAsync(transcription, cancellationToken).ConfigureAwait(false);
        
        var record = new PhoneOrderRecord { SessionId = Guid.NewGuid().ToString(), Restaurant = recordInfo.Restaurant, TranscriptionText = transcription, Language = SelectLanguageEnum(detection.Language), CreatedDate = recordInfo.OrderDate.AddHours(-8), Status = PhoneOrderRecordStatus.Recieved };

        if (await CheckPhoneOrderRecordDurationAsync(command.RecordContent, cancellationToken).ConfigureAwait(false))
        {
            await AddPhoneOrderRecordAsync(record, PhoneOrderRecordStatus.NoContent, cancellationToken).ConfigureAwait(false);
            
            return;
        }
        
        record.Url = await UploadRecordFileAsync(command.RecordName, command.RecordContent, cancellationToken).ConfigureAwait(false);
        
        Log.Information($"Phone order record file url: {record.Url}",  record.Url);
        
        if (string.IsNullOrEmpty(record.Url))
        {
            await AddPhoneOrderRecordAsync(record, PhoneOrderRecordStatus.NoContent, cancellationToken).ConfigureAwait(false);
            
            return;
        }

        record.TranscriptionJobId = await CreateSpeechMaticsJobAsync(command.RecordContent, command.RecordName, detection.Language, cancellationToken).ConfigureAwait(false);
        
        await AddPhoneOrderRecordAsync(record, PhoneOrderRecordStatus.Diarization, cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> CheckOrderExistAsync(DateTimeOffset createdDate, CancellationToken cancellationToken)
    {
        return (await _phoneOrderDataProvider.GetPhoneOrderRecordAsync(createdDate: createdDate, cancellationToken: cancellationToken).ConfigureAwait(false)).Any();
    }

    public TranscriptionLanguage SelectLanguageEnum(string language)
    {
        return language switch
        {
            "zh" => TranscriptionLanguage.Chinese,
            "en" => TranscriptionLanguage.English,
            "es" => TranscriptionLanguage.Spanish,
            _ => TranscriptionLanguage.Chinese
        };
    }

    public async Task ExtractPhoneOrderRecordAiMenuAsync(
        List<SpeechMaticsSpeakInfoDto> phoneOrderInfo, PhoneOrderRecord record, byte[] audioContent, CancellationToken cancellationToken)
    {
        if (phoneOrderInfo is { Count: 0 }) return;
        
        phoneOrderInfo = await HandlerConversationFirstSentenceAsync(phoneOrderInfo, record, audioContent, cancellationToken).ConfigureAwait(false);
        
        Log.Information("Phone order record info: {@phoneOrderInfo}", phoneOrderInfo);
        
        var (goalText, tip) = await PhoneOrderTranscriptionAsync(phoneOrderInfo, record, audioContent, cancellationToken).ConfigureAwait(false);
        
        await ExtractPhoneOrderShoppingCartAsync(goalText, record, cancellationToken).ConfigureAwait(false);
        
        record.Tips = tip;
        record.Status = PhoneOrderRecordStatus.Sent;
        record.TranscriptionText = goalText;
        
        await _phoneOrderDataProvider.UpdatePhoneOrderRecordsAsync(record, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task<AddOrUpdateManualOrderResponse> AddOrUpdateManualOrderAsync(AddOrUpdateManualOrderCommand command, CancellationToken cancellationToken)
    {
        var orderId = long.Parse(command.OrderId); 
        
        Log.Information($"Add manual order: {orderId}", orderId);
        
        var manualOrder = await _easyPosClient.GetOrderAsync(orderId, command.Restaurant, cancellationToken).ConfigureAwait(false);

        Log.Information("Get order response: response: {@manualOrder}", manualOrder);
        
        if (manualOrder.Data == null) return  new AddOrUpdateManualOrderResponse
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

    private async Task<List<SpeechMaticsSpeakInfoDto>> HandlerConversationFirstSentenceAsync(List<SpeechMaticsSpeakInfoDto> phoneOrderInfos, PhoneOrderRecord record, byte[] audioContent, CancellationToken cancellationToken)
    {
        var originText = await SplitAudioAsync(audioContent, record, phoneOrderInfos[0].StartTime * 1000, phoneOrderInfos[0].EndTime * 1000,
            TranscriptionFileType.Wav, cancellationToken).ConfigureAwait(false);
        
        if (await CheckAudioFirstSentenceIsRestaurantAsync(originText, cancellationToken).ConfigureAwait(false)) return phoneOrderInfos;
        
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
                    conversations.Add(new PhoneOrderConversation { RecordId = record.Id, Question = originText, Order = conversationIndex });
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
                    RecordId = record.Id
                });
            }

            ShiftConversations(conversations);
        }
        
        goalTextsString = ProcessConversation(conversations);
        
        await _phoneOrderDataProvider.AddPhoneOrderConversationsAsync(conversations, true, cancellationToken).ConfigureAwait(false);

        return (goalTextsString, conversations.First().Question);
    }

    private static string ProcessConversation(List<PhoneOrderConversation> conversations)
    {
        var goalTextsString = "";
        
        foreach (var conversation in conversations.ToList())
        {
            if (string.IsNullOrEmpty(conversation.Answer) && string.IsNullOrEmpty(conversation.Question)) conversations.Remove(conversation);
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
        var completionResult = await _smartiesClient.PerformQueryAsync( new AskGptRequest
        {
            Messages = new List<CompletionsRequestMessageDto>
            {
                new () {
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
                new ()
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
        var completionResult = await _smartiesClient.PerformQueryAsync( new AskGptRequest
        {
            Messages = new List<CompletionsRequestMessageDto>
            {
                new () {
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
                new ()
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
        }
        
        var lastConversation = conversations[^1];
        lastConversation.Question = lastConversation.Answer;
        lastConversation.Answer = null;
        lastConversation.Order = conversations.Count - 1;
        
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
    
    private PhoneOrderRecordInformationDto ExtractPhoneOrderRecordInfoFromRecordName(string recordName)
    {
        var time = string.Empty;
        var phoneNumber = string.Empty;
        
        var regexInOut = new Regex(@"(?:in-(\d+)|out-\d+-(\d+))-.*-(\d+)\.\d+\.wav");
        var match = regexInOut.Match(recordName);

        if (match.Success)
        {
            time = match.Groups[3].Value;
            phoneNumber = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
        }

        return new PhoneOrderRecordInformationDto
        {
            OrderDate = DateTimeOffset.FromUnixTimeSeconds(long.Parse(time)),
            Restaurant = phoneNumber[0] switch
            {
                '3' or '6' => PhoneOrderRestaurant.JiangNanChun,
                '5' or '7' => PhoneOrderRestaurant.XiangTanRenJia,
                '8' or '9' or '2' or '0' => PhoneOrderRestaurant.MoonHouse,
                _ => throw new Exception("Phone Number not exist")
            },
            WorkWeChatRobotKey = phoneNumber[0] switch
            {
                '3' or '6' => _phoneOrderSetting.GetSetting("江南春"),
                '5' or '7' =>  _phoneOrderSetting.GetSetting("湘潭人家"),
                '8' or '9' or '2' or '0' => _phoneOrderSetting.GetSetting("福满楼"),
                _ => throw new Exception("Phone Number not exist")
            }
        };
    }
    
    private async Task UpdatePhoneOrderRecordSpecificFieldsAsync(int recordId, int modifiedBy, string tips, CancellationToken cancellationToken)
    {
        var record = (await _phoneOrderDataProvider.GetPhoneOrderRecordAsync(recordId, cancellationToken: cancellationToken).ConfigureAwait(false)).FirstOrDefault();

        record.Tips = tips;
        record.LastModifiedBy = modifiedBy;
        record.LastModifiedDate = DateTimeOffset.Now;

        await _phoneOrderDataProvider.UpdatePhoneOrderRecordsAsync(record, cancellationToken: cancellationToken).ConfigureAwait(false);
    }
     
    private async Task<PhoneOrderDetailDto> GetSimilarRestaurantByRecordAsync(PhoneOrderRecord record, PhoneOrderDetailDto foods, CancellationToken cancellationToken)
    {
        var result = new PhoneOrderDetailDto { FoodDetails = new List<FoodDetailDto>() };
        var restaurant = await _restaurantDataProvider.GetRestaurantByNameAsync(record.Restaurant.GetDescription(), cancellationToken).ConfigureAwait(false);

        var tasks = foods.FoodDetails.Select(async foodDetail =>
        {
            var similarFoodsResponse = await _vectorDb.GetSimilarListAsync(
                restaurant.Id.ToString(), foodDetail.FoodName, minRelevance: 0.4, cancellationToken: cancellationToken).ToListAsync(cancellationToken);

            if (similarFoodsResponse.Count == 0) return null;
            
            var payload = similarFoodsResponse.First().Item1.Payload[VectorDbStore.ReservedRestaurantPayload].ToString();
            
            if (string.IsNullOrEmpty(payload)) return null;
            
            foodDetail.FoodName = JsonConvert.DeserializeObject<RestaurantPayloadDto>(payload).Name;
            foodDetail.Price = (double)JsonConvert.DeserializeObject<RestaurantPayloadDto>(payload).Price;
            
            return foodDetail;
        }).ToList();

        var completedTasks = await Task.WhenAll(tasks);
        
        result.FoodDetails.AddRange(completedTasks.Where(fd => fd != null));

        return result;
    }
    
    public async Task<string> SplitAudioAsync(byte[] file, PhoneOrderRecord record, double speakStartTimeVideo, double speakEndTimeVideo, TranscriptionFileType fileType = TranscriptionFileType.Wav, CancellationToken cancellationToken = default)
    {
        if (file == null) return null;
        
        var audioBytes = await _ffmpegService.ConvertFileFormatAsync(file, fileType, cancellationToken).ConfigureAwait(false);
    
        var splitAudios = await _ffmpegService.SpiltAudioAsync(audioBytes, speakStartTimeVideo, speakEndTimeVideo, cancellationToken).ConfigureAwait(false);
        
        var transcriptionResult = new StringBuilder();
        
        foreach (var reSplitAudio in splitAudios)
        {
            var transcriptionResponse = await _speechToTextService.SpeechToTextAsync(
                reSplitAudio, record.Language, TranscriptionFileType.Wav, TranscriptionResponseFormat.Text, 
                SelectPrompt(record.Restaurant), cancellationToken: cancellationToken).ConfigureAwait(false);
            
            transcriptionResult.Append(transcriptionResponse);
        }
        
        Log.Information("Transcription result {Transcription}", transcriptionResult.ToString());
        
        return transcriptionResult.ToString();
    }

    private string SelectPrompt(PhoneOrderRestaurant restaurant)
    {
        return restaurant switch
        {
            PhoneOrderRestaurant.MoonHouse => "Moon, Hello Moon house, Moon house",
            PhoneOrderRestaurant.XiangTanRenJia => "你好,湘里人家",
            PhoneOrderRestaurant.JiangNanChun => "你好,江南春",
            _ => ""
        };
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
                new SpeechMaticsNotificationConfigDto{
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
            _ => SpeechMaticsLanguageType.Auto
        };
    }
    
    private async Task ExtractPhoneOrderShoppingCartAsync(string goalTexts, PhoneOrderRecord record, CancellationToken cancellationToken)
    {
        var shoppingCart = await GetOrderDetailsAsync(goalTexts, cancellationToken).ConfigureAwait(false);

        shoppingCart = await GetSimilarRestaurantByRecordAsync(record, shoppingCart, cancellationToken).ConfigureAwait(false);
        
        var items = shoppingCart.FoodDetails.Select(x => new PhoneOrderOrderItem
        {
            RecordId = record.Id,
            FoodName = x.FoodName,
            Quantity = x.Count ?? 0,
            Price = x.Price,
            Note = x.Remark
        }).ToList();

        if (items.Any())
            await _phoneOrderDataProvider.AddPhoneOrderItemAsync(items, true, cancellationToken).ConfigureAwait(false);
    }

    private async Task<PhoneOrderDetailDto> GetOrderDetailsAsync(string query, CancellationToken cancellationToken)
    {
        var completionResult = await _smartiesClient.PerformQueryAsync( new AskGptRequest
        {
            Messages = new List<CompletionsRequestMessageDto>
            {
                new () {
                    Role = "system",
                    Content = new CompletionsStringContent("你是一款高度理解语言的智能助手，根据所有对话提取Client的food_details。" +
                                       "--规则：" +
                                       "1.根据全文帮我提取food_details，count是菜品的数量且为整数，如果你不清楚数量的时候，count默认为1，remark是对菜品的备注" +
                                       "2.根据对话中Client的话为主提取food_details" +
                                       "3.不要出现重复菜品，如果有特殊的要求请标明数量，例如我要两份粥，一份要辣，则标注一份要辣" +
                                       "注意用json格式返回；规则：{\"food_details\": [{\"food_name\": \"菜品名字\",\"count\":减少的数量（负数）, \"remark\":null}]}}" +
                                       "-样本与输出：" +
                                       "input: Restaurant: . Client:Hi, 我可以要一個外賣嗎? Restaurant:可以啊,要什麼? Client: 我要幾個特價午餐,要一個蒙古牛,要一個蛋花湯跟這個,再要一個椒鹽排骨蛋花湯,然後再要一個魚香肉絲,不要辣的蛋花湯。Restaurant:可以吧。Client:然后再要一个春卷 再要一个法式柠檬柳粒。out:{\"food_details\": [{\"food_name\":\"蒙古牛\",\"count\":1, \"remark\":null},{\"food_name\":\"蛋花湯\",\"count\":3, \"remark\":},{\"food_name\":\"椒鹽排骨\",\"count\":1, \"remark\":null},{\"food_name\":\"魚香肉絲\",\"count\":1, \"remark\":null},{\"food_name\":\"春卷\",\"count\":1, \"remark\":null},{\"food_name\":\"法式柠檬柳粒\",\"count\":1, \"remark\":null}]}" +
                                       "input: Restaurant: Moon house Client: Hi, may I please have a compound chicken with steamed white rice? Restaurant: Sure, 10 minutes, thank you. Client: Hold on, I'm not finished, I'm not finished Restaurant: Ok, Sir, First Sir, give me your phone number first One minute, One minute, One minute, One minute, Ok, One minute, One minute Client: Okay Restaurant: Ok, 213 Client: 590-6995 You guys want me to order something for you guys? Restaurant: 295, Rm Client: 590-2995 Restaurant: Ah, no, yeah, maybe they have an old one, so, that's why. Client: Okay, come have chicken with cream white rice Restaurant: Bye bye, okay, something else? Client: Good morning, Kidman Restaurant: Okay Client: What do you want?  An order of mongolian beef also with cream white rice please Restaurant: Client: Do you want something, honey?  No, on your plate, you want it?  Let's go to the level, that's a piece of meat.  Let me get an order of combination fried rice, please. Restaurant: Sure, Question is how many wires do we need? Client: Maverick, do you want to share a chicken chow mein with me, for later?  And a chicken chow mein, please.  So that's one compote chicken, one orange chicken, one mingolian beef, one combination rice, and one chicken chow mein, please. Restaurant: Okay, let's see how many, one or two Client: Moon house Restaurant: Tube Tuner, right? Client: Can you separate, can you put in a bag by itself, the combination rice and the mongolian beef with one steamed rice please, because that's for getting here with my daughter. Restaurant: Okay, so let me know.  Okay, so I'm going to leave it.  Okay.  Got it Client: Moon house Restaurant: I'll make it 20 minutes, OK?  Oh, I'm sorry, you want a Mangaloreng beef on a fried rice and one steamed rice separate, right?  Yes.  OK. Client: combination rice, the mongolian beans and the steamed rice separate in one bag. Restaurant: Okay, Thank you Thank you out:{\"food_details\":[{\"food_name\":\"compound chicken\",\"count\":1, \"remark\":null},{\"food_name\":\"orange chicken\",\"count\":1, \"remark\":null},{\"food_name\":\"mongolian beef\",\"count\":1, \"remark\":null},{\"food_name\":\"chicken chow mein\",\"count\":1, \"remark\":null},{\"food_name\":\"combination rice\",\"count\":1, \"remark\":null},{\"food_name\":\"white rice\",\"count\":2, \"remark\":null}]}"
                                       )
                    
                },
                new ()
                {
                    Role = "user",
                    Content = new CompletionsStringContent($"input:{query}, output:")
                }
            },
            Model = OpenAiModel.Gpt4o,
            ResponseFormat = new () { Type = "json_object" }
        }, cancellationToken).ConfigureAwait(false);
        
        return completionResult.Data.Response == null ? null : JsonConvert.DeserializeObject<PhoneOrderDetailDto>(completionResult.Data.Response);
    }
    
    private async Task<string> CreateSpeechMaticsJobAsync(byte[] recordContent, string recordName, string language, CancellationToken cancellationToken)
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
}