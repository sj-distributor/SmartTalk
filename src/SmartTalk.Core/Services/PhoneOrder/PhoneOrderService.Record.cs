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
using SmartTalk.Core.Extensions;
using SmartTalk.Core.Services.Caching;
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
using SmartTalk.Messages.Dto.Restaurant;
using TranscriptionFileType = SmartTalk.Messages.Enums.STT.TranscriptionFileType;
using TranscriptionResponseFormat = SmartTalk.Messages.Enums.STT.TranscriptionResponseFormat;

namespace SmartTalk.Core.Services.PhoneOrder;

public partial interface IPhoneOrderService
{
    Task<GetPhoneOrderRecordsResponse> GetPhoneOrderRecordsAsync(GetPhoneOrderRecordsRequest request, CancellationToken cancellationToken);

    Task ReceivePhoneOrderRecordAsync(ReceivePhoneOrderRecordCommand command, CancellationToken cancellationToken);

    Task ExtractPhoneOrderRecordAiMenuAsync(List<SpeechMaticsSpeakInfoDto> phoneOrderInfo, PhoneOrderRecord record, byte[] audioContent, CancellationToken cancellationToken);

    Task<AddManualOrderResponse> AddManualOrderAsync(AddManualOrderCommand command, CancellationToken cancellationToken);
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
        
        var transcription = await _speechToTextService.SpeechToTextAsync(
            command.RecordContent, fileType: TranscriptionFileType.Wav, responseFormat: TranscriptionResponseFormat.Text, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrEmpty(transcription) || transcription.Length < 15 || transcription.Contains("GENERAL") || transcription.Contains("感謝收看") || transcription.Contains("訂閱") || transcription.Contains("点赞") || transcription.Contains("立場") || transcription.Contains("字幕") || transcription.Contains("結束") || transcription.Contains("謝謝觀看") || transcription.Contains("幕後大臣") || transcription == "醒醒" || transcription == "跟著我" || transcription.Contains("政經關峻") || transcription.Contains("您拨打的电话") || transcription.Contains("Mailbox memory is full") || transcription.Contains("amazon", StringComparison.InvariantCultureIgnoreCase) || transcription.Contains("We're sorry, your call did not go through.", StringComparison.InvariantCultureIgnoreCase) || transcription.Contains("Verizon Wireless", StringComparison.InvariantCultureIgnoreCase) || transcription.Contains("Beep", StringComparison.InvariantCultureIgnoreCase) || transcription.Contains("USPS customer service center", StringComparison.InvariantCultureIgnoreCase) || transcription.Contains("not go through", StringComparison.InvariantCultureIgnoreCase) || transcription.Contains("stop serving in two hours", StringComparison.InvariantCultureIgnoreCase) || transcription.Contains("Welcome to customer support", StringComparison.InvariantCultureIgnoreCase) || transcription.Contains("For the upcoming flight booking", StringComparison.InvariantCultureIgnoreCase) || transcription.Contains("Please check and dial again.", StringComparison.InvariantCultureIgnoreCase) || transcription.Contains("largest cable TV network", StringComparison.InvariantCultureIgnoreCase) || transcription.Contains("拨打的用户暂时无法接通") || transcription.Contains("您有一份国际快递即将退回")) return;
        
        var detection = await _translationClient.DetectLanguageAsync(transcription, cancellationToken).ConfigureAwait(false);
        
        var record = new PhoneOrderRecord { SessionId = Guid.NewGuid().ToString(), Restaurant = recordInfo.Restaurant, TranscriptionText = transcription, Language = SelectLanguageEnum(detection.Language), CreatedDate = recordInfo.OrderDate.AddHours(-7), Status = PhoneOrderRecordStatus.Recieved };

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
        
        var transcriptionJobIdJObject = JObject.Parse(await CreateTranscriptionJobAsync(command.RecordContent, command.RecordName, detection.Language, cancellationToken).ConfigureAwait(false)) ;
        
        var transcriptionJobId = transcriptionJobIdJObject["id"]?.ToString();
        
        record.TranscriptionJobId = transcriptionJobId;
     
        Log.Information("Phone order record transcriptionJobId: {@transcriptionJobId}", transcriptionJobId);
        
        await AddPhoneOrderRecordAsync(record, PhoneOrderRecordStatus.Diarization, cancellationToken).ConfigureAwait(false);
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
        
        var (goalTexts, shoppingCart, conversations) = await PhoneOrderTranscriptionAsync(phoneOrderInfo, record, audioContent, cancellationToken).ConfigureAwait(false);
        
        record.Tips = goalTexts.First();
        record.Status = PhoneOrderRecordStatus.Sent;
        record.TranscriptionText = string.Join("\n", goalTexts);
        conversations.Where(x => string.IsNullOrEmpty(x.Answer)).ForEach(x => x.Answer = string.Empty);
        
        await _phoneOrderDataProvider.UpdatePhoneOrderRecordsAsync(record, cancellationToken: cancellationToken).ConfigureAwait(false);
        await _phoneOrderDataProvider.AddPhoneOrderConversationsAsync(conversations, true, cancellationToken).ConfigureAwait(false);

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

    public async Task<AddManualOrderResponse> AddManualOrderAsync(AddManualOrderCommand command, CancellationToken cancellationToken)
    {
        var manualOrder = await _easyPosClient.GetOrderAsync(command.OrderId, command.Restaurant, cancellationToken).ConfigureAwait(false);

        Log.Information("Get order response: response: {@manualOrder}", manualOrder);
        
        if (manualOrder.Data == null) return  new AddManualOrderResponse();
        
        var oderItems = manualOrder.Data.Order.OrderItems.Select(x =>
        {
            return new PhoneOrderOrderItem
            {
                RecordId = command.RecordId,
                FoodName = x.Localizations.First(c => c.Field == "name" && c.languageCode == "zh_CN").Value,
                Quantity = x.Quantity,
                Price = x.Price
            };
        }).ToList();
        
        await _phoneOrderDataProvider.AddPhoneOrderItemAsync(oderItems, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        return new AddManualOrderResponse
        {
            Data = _mapper.Map<List<PhoneOrderOrderItemDto>>(oderItems)
        };
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

    private async Task<(List<string>, PhoneOrderDetailDto, List<PhoneOrderConversation>)> PhoneOrderTranscriptionAsync(
        List<SpeechMaticsSpeakInfoDto> phoneOrderInfo, PhoneOrderRecord record, byte[] audioContent, CancellationToken cancellationToken)
    {
        var conversationIndex = 0;
        var recordingContext = new List<string>();
        var goalTexts = new List<string>();
        var shoppingCart = new PhoneOrderDetailDto();
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
                {
                    originText = "";
                }
                
                Log.Information("Phone Order transcript originText: {originText}", originText);
                    
                goalTexts.Add((speakDetail.Role == PhoneOrderRole.Restaurant
                    ? PhoneOrderRole.Restaurant.ToString()
                    : PhoneOrderRole.Client.ToString()) + ": " + originText);

                if (speakDetail.Role == PhoneOrderRole.Restaurant)
                    conversations.Add(new PhoneOrderConversation { RecordId = record.Id, Question = originText, Order = conversationIndex });
                else
                {
                    var recordContext = await _cacheManager.GetAsync<string>($"{record.Id}", new RedisCachingSetting(), cancellationToken).ConfigureAwait(false);
                    
                    var intent = await RecognizeIntentAsync(originText, recordContext, cancellationToken).ConfigureAwait(false);

                    PhoneOrderDetailDto extractFoods = null;

                    switch (intent)
                    {
                        case PhoneOrderIntent.AddOrder:
                            extractFoods = await AddOrderDetailAsync(originText, recordContext, cancellationToken).ConfigureAwait(false);
                            extractFoods = await GetSimilarRestaurantByRecordAsync(record, extractFoods, cancellationToken).ConfigureAwait(false);
                            CheckOrAddToShoppingCart(shoppingCart.FoodDetails, extractFoods.FoodDetails);
                            break;
                        case PhoneOrderIntent.ReduceOrder:
                            extractFoods = await ReduceOrderDetailAsync(shoppingCart.FoodDetails, originText, recordContext, cancellationToken).ConfigureAwait(false);
                            extractFoods = await GetSimilarRestaurantByRecordAsync(record, extractFoods, cancellationToken).ConfigureAwait(false);
                            CheckOrReduceFromShoppingCart(shoppingCart.FoodDetails, extractFoods.FoodDetails);
                            break;
                    }

                    conversations[conversationIndex].Intent = intent;
                    conversations[conversationIndex].Answer = originText;
                    if (extractFoods != null)
                        conversations[conversationIndex].ExtractFoodItem = JsonConvert.SerializeObject(extractFoods.FoodDetails);

                    conversationIndex++;
                }

                if (recordingContext.Count == 8)
                    recordingContext.RemoveRange(0, 2);
                
                recordingContext.Add($"{speakDetail.Role.ToString()}: {originText}");
                
                await _cacheManager.SetAsync(
                    $"{record.Id}", string.Join("\n", recordingContext), new RedisCachingSetting(expiry: TimeSpan.FromHours(1)), cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                record.Status = PhoneOrderRecordStatus.Exception;

                Log.Information("transcription error: {ErrorMessage}", ex.Message);
            }
        }

        Log.Information("Phone order conversation: {@conversations}, shopping cart: {@shoppingCart}", conversations, shoppingCart);

        return (goalTexts, shoppingCart, conversations);
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

        return timeSpan.TotalSeconds < 3 || timeSpan.Seconds == 14 || timeSpan.Seconds == 10;
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
                '8' or '9' => PhoneOrderRestaurant.MoonHouse,
                _ => throw new Exception("Phone Number not exist")
            },
            WorkWeChatRobotKey = phoneNumber[0] switch
            {
                '3' or '6' => _phoneOrderSetting.GetSetting("江南春"),
                '5' or '7' =>  _phoneOrderSetting.GetSetting("湘潭人家"),
                '8' or '9' => _phoneOrderSetting.GetSetting("福满楼"),
                _ => throw new Exception("Phone Number not exist")
            }
        };
    }
    
    private async Task UpdatePhoneOrderRecordSpecificFieldsAsync(int recordId, int modifiedBy, string tips, CancellationToken cancellationToken)
    {
        var record = await _phoneOrderDataProvider.GetPhoneOrderRecordByIdAsync(recordId, cancellationToken).ConfigureAwait(false);

        record.Tips = tips;
        record.LastModifiedBy = modifiedBy;

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
    
     private async Task<PhoneOrderIntent> RecognizeIntentAsync(string input, string recordContext, CancellationToken cancellationToken)
    {
        var response = await _smartiesClient.PerformQueryAsync(new AskGptRequest
        {
            Messages = new List<CompletionsRequestMessageDto>
            {
                new ()
                {
                    Role = "system",
                    Content = new CompletionsStringContent("你是一个智能助手，能够根据用户的输入结合上下文识别出相应的意图。" +
                                                           "--规则：" +
                                                           "1.根据上下文，分析当前用户意图" +
                                                           "2. 如果打招呼intent和其他intent同时存在的时候，优先选择其他intent，例如\"hello，我要一份蛋炒饭？\"，应该识别成【加单】的intent而不是打招呼" +
                                                           "3. 如果下单intent中输入有菜品，都应该落入加单intent中而不是下单" +
                                                           "4.请根据用户的实际输入进行意图识别，并返回一个标识数字表示识别出的意图。以下是可能的意图和对应的样本：\n" +
                                                           " {\n \"0\": {\"intent\": \"闲聊\", \"sample\": [\"你今日开心吗？, \"今天天气点呀？\", \"你做紧d咩？\"]},\n " +
                                                           "\"1\": {\"intent\": \"加单(点菜)\", \"sample\": [\"帮我拿个白粥\", \"仲要两份椒盐鸡翼\", \"再嚟个扬州炒饭\", \"多個海鮮炒面同埋炸鸡翼\", \"我要叉烧炒饭\"]},\n" +
                                                           " \"2\": {\"intent\": \"减单\", \"sample\": [\"你帮我去一个菠萝油,留一个给老婆\", \"唔要啱啱嗰道菜\", \"取消啱啱點嘅全部菜\", \"唔要魚旦啦\", \"乜都唔要啦\", \"不要刚刚点的了\", \"取消扬州炒饭\", \"取消扬州炒饭\"]},\n根据以上意图和样本，请识别以下用户输入的意图，并返回相应的意图标识符（例如 0或1）。\n" +
                                                           "上下文:" +
                                                           "Restaurant: ," +
                                                           "Client:Hi,can I place an order for pickup? " +
                                                           "Restaurant:phone number," +
                                                           "Client:818-336-2982, " +
                                                           "Restaurant:Ok,what's your order?" +
                                                           "Client:Do you have chicken fried rice?" +
                                                           "Restaurant:Okay; \n" +
                                                           "当前用户意图:and that's it.output:1 \n" +
                                                           "当前用户意图:我想要一罐可乐，一份扬州炒饭，一份湿炒牛河，output:1\n" +
                                                           "当前用户意图:有无咩推荐啊，output:0\n" +
                                                           "当前用户意图:我落左咩单，全部讲来听下，output:0\n" +
                                                           "当前用户意图:落单之前的菜，output:0\n" +
                                                           "当前用户意图:下单一份炒饭，output:1\n" +
                                                           "当前用户意图:下单炒饭，output:1\n" )
                },
                new ()
                {
                    Role = "user",
                    Content = new CompletionsStringContent($"上下文:{recordContext} \n当前用户意图: {input}\n输出意图数字:")
                }
            },
            Model = OpenAiModel.Gpt4o
        }, cancellationToken).ConfigureAwait(false); 

        var responseContent = response.Data.Response;
        
        Log.Information("OpenAI classify client intent response: {responseContent}", responseContent);
        
        return int.TryParse(responseContent, out var intent) && Enum.IsDefined(typeof(PhoneOrderIntent), intent) ? (PhoneOrderIntent)intent : PhoneOrderIntent.Default;
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
    
    private static void CheckOrAddToShoppingCart(List<FoodDetailDto> shoppingCart, List<FoodDetailDto> foods)
    {
        var hasFoods = new List<FoodDetailDto>();
        
        foods.ForEach(x =>
        {
            if (!string.IsNullOrEmpty(x.Remark))
            {
                hasFoods.Add(x);
            }
            else
            {
                if (shoppingCart.Any(t => t.FoodName == x.FoodName && string.IsNullOrEmpty(t.Remark)))
                {
                    foreach (var food in shoppingCart)
                    {
                        if(food.FoodName == x.FoodName && string.IsNullOrEmpty(food.Remark))
                            food.Count += x.Count;
                    }
                }
                else
                {
                    hasFoods.Add(x);
                }
            }
        });

        if (hasFoods.Any()) shoppingCart.AddRange(hasFoods);
        
        Log.Information("Shopping cart after add: {ShoppingCart}", JsonConvert.SerializeObject(shoppingCart));
    }
    
    private async Task<PhoneOrderDetailDto?> AddOrderDetailAsync(string query, string recordContext, CancellationToken cancellationToken)
    {
        var completionResult = await _smartiesClient.PerformQueryAsync( new AskGptRequest
        {
            Messages = new List<CompletionsRequestMessageDto>
            {
                new () {
                    Role = "system",
                    Content = new CompletionsStringContent("你是一款高度理解语言的智能助手，结合上下文专门从当前语句用于识别和处理电话订单。" +
                                       "--规则：" +
                                       "1.根据上下文，结合用户当前问题帮我补全food_details，count是菜品的数量，如果你不清楚数量的时候，count默认为1，remark是对菜品的备注" +
                                       "2.如果当用户的请求的菜品不在菜单上时，也需要返回菜品种类，菜品名称数量和备注。 " +
                                       "注意用json格式返回；规则：{\"food_details\": [{\"food_name\": {\"小吃\": [\"炸鸡翼\",\"港式咖喱魚旦\",\"椒盐鸡翼\",\"菠萝油\"]," +
                                       "-样本与输出：" +
                                       "上下文:" +
                                       "Restaurant: ," +
                                       "Client:Hi,can I place an order for pickup? " +
                                       "Restaurant:phone number," +
                                       "Client:818-336-2982, " +
                                       "Restaurant:Ok,what's your order?" +
                                       "Client:Do you have chicken fried rice?" +
                                       "Restaurant:Okay; \n" +
                                       "用户当前问题:and that's it." +
                                       "output:{\"food_details\": [{\"food_name\": \"rice\",\"count\":1, \"remark\":null}]}}" +
                                       "用户当前问题:我要两份皮蛋瘦肉粥，有一个不要皮蛋; " +
                                       "output:{\"food_details\": [{\"food_name\": \"皮蛋瘦肉粥\",\"count\":2, \"remark\":一份不要皮蛋}]}}\n" +
                                       "用户当前问题:要可乐; " +
                                       "output:{\"food_details\": [{\"food_name\": \"可乐\",\"count\":1, \"remark\":null}]}}\n" +
                                       "用户当前问题:我要四个扬州炒饭，有两份不要葱，还要一份草莓绵绵冰; " +
                                       "output:{\"food_details\": [{\"food_name\": \"扬州炒饭\",\"count\":4, \"remark\":两份不要葱},{\"food_category\": \"其他\", \"food_name\": \"草莓绵绵冰\",\"count\":1, \"remark\":null}]}}\n" +
                                       "用户当前问题:要一个炸鸡翼和一个稠一点的白粥 " +
                                       "output:{\"food_details\": [{\"food_name\": \"明火白粥\",\"count\":1, \"remark\":稠一点},{\"food_category\": \"小吃\", \"food_name\": \"炸鸡翼\",\"count\":1, \"remark\":null}]}}\n"
                                       )
                    
                },
                new ()
                {
                    Role = "user",
                    Content = new CompletionsStringContent($"上下文:{recordContext} \n 用户当前问题: {query}, output:")
                }
            },
            Model = OpenAiModel.Gpt4o,
            ResponseFormat = new () { Type = "json_object" }
        }, cancellationToken).ConfigureAwait(false);
        
        Log.Information("AddOrderDetail openaiResponse:" + completionResult.Data.Response);
        
        return completionResult.Data.Response == null ? null : JsonConvert.DeserializeObject<PhoneOrderDetailDto>(completionResult.Data.Response);
    }
    
    private void CheckOrReduceFromShoppingCart(List<FoodDetailDto> shoppingCart, List<FoodDetailDto> foods)
    {
        var hasFoods = new List<FoodDetailDto>();
        
        foods.ForEach(x =>
        {
            if (shoppingCart.Any(s => x.FoodName.Trim() == s.FoodName.Trim())) hasFoods.Add(x);
        });
        
        shoppingCart.ForEach(x =>
        {
            if (hasFoods.Any(s => s.FoodName.Trim() == x.FoodName.Trim()))
                x.Count += hasFoods.First(s => s.FoodName.Trim() == x.FoodName.Trim()).Count;
        });

        shoppingCart = shoppingCart.Where(x => x.Count > 0).ToList();
        
        Log.Information("Shopping cart after reduce: {ShoppingCart}", JsonConvert.SerializeObject(shoppingCart));
    }
    
    private async Task<PhoneOrderDetailDto> ReduceOrderDetailAsync(List<FoodDetailDto> shoppingCart, string query, string recordContext, CancellationToken cancellationToken)
    {
        var shoppingCar = JsonConvert.SerializeObject(shoppingCart, Formatting.Indented);
        
        var completionResult = await _smartiesClient.PerformQueryAsync( new AskGptRequest
        {
            Messages = new List<CompletionsRequestMessageDto>
            {
                new () {
                    Role = "system",
                    Content = new CompletionsStringContent("你是一款高度理解语言的智能助手，专门用于识别和处理电话订单。" +
                                                           "--规则" +
                                                           $"1.根据上下文结合我目前的购物车的内容和输入，来帮我补全food_details，count是菜品的数量，如果你不清楚数量的时候，count默认为-1，购物车内容如下：{shoppingCar}，remark固定为null;" +
                                                           "2.如果当用户的请求的菜品不在菜单上时，也需要返回菜品种类，菜品名称数量和备注。" +
                                                           "注意用json格式返回；规则：{\"food_details\": [{\"food_name\": \"菜品名字\",\"count\":减少的数量（负数）, \"remark\":null}]}}" +
                                                           "- 样本与输出：\n" +
                                                           "上下文:" +
                                                           "Restaurant: ," +
                                                           "Client:Hi,can I place an order for pickup? " +
                                                           "Restaurant:phone number," +
                                                           "Client:818-336-2982, " +
                                                           "Restaurant:Ok,what's your order?" +
                                                           "Client:Do you have chicken fried rice?" +
                                                           "Restaurant:Okay; " +
                                                           "Client:and that's it." +
                                                           "Restaurant:Okay; \n" +
                                                           "用户当前问题:I don't want the food I just ordered. output:{\"food_details\": [{\"food_name\": \"chicken fried rice\",\"count\":-1, \"remark\":null}]}}" +
                                                           "用户当前问题:你帮我去一个菠萝油,留一个给老婆 output:{\"food_details\": [{\"food_name\": \"菠萝油\",\"count\":-1, \"remark\":null}]}}\n" +
                                                           "用户当前问题:刚刚点的那一份皮蛋瘦肉粥不要了 output:{\"food_details\": [{\"food_name\": \"皮蛋瘦肉粥\",\"count\":-1, \"remark\":null}]}}\n" +
                                                           "用户当前问题:全部不要了 output: null\n" +
                                                           "（假设购物车里有三份扬州炒饭）" +
                                                           "用户当前问题:刚刚点的扬州炒饭不要了 output:{\"food_details\": [{\"food_name\": \"扬州炒饭\",\"count\":-3, \"remark\":null}]}}\n")
                },
                new ()
                {
                    Role = "user",
                    Content = new CompletionsStringContent($"上下文:{recordContext}\n用户当前问题: {query}, output:")
                }
            },
            Model = OpenAiModel.Gpt4o,
            ResponseFormat = new () { Type = "json_object" }
        }, cancellationToken).ConfigureAwait(false);
        
        Log.Information("ReduceOrderDetail openaiResponse:" + completionResult.Data.Response);
        
        return completionResult.Data.Response == null ? null : JsonConvert.DeserializeObject<PhoneOrderDetailDto>(completionResult.Data.Response);
    }
}