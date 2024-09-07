using System.Net.Http.Headers;
using System.Text;
using Serilog;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using OpenAI;
using OpenAI.Managers;
using Smarties.Messages.DTO.OpenAi;
using Smarties.Messages.Enums.OpenAi;
using Smarties.Messages.Requests.Ask;
using SmartTalk.Core.Domain.PhoneOrder;
using SmartTalk.Core.Extensions;
using SmartTalk.Messages.Dto.PhoneOrder;
using SmartTalk.Messages.Dto.Attachments;
using SmartTalk.Messages.Enums.PhoneOrder;
using SmartTalk.Messages.Commands.PhoneOrder;
using SmartTalk.Messages.Requests.PhoneOrder;
using SmartTalk.Messages.Commands.Attachments;
using SmartTalk.Messages.Constants;
using SmartTalk.Messages.Dto.Restaurant;
using SmartTalk.Messages.Dto.SpeechMatics;
using SmartTalk.Messages.Dto.WebSocket;
using SmartTalk.Messages.Dto.WeChat;
using SmartTalk.Messages.Enums.SpeechMatics;
using SmartTalk.Messages.Enums.STT;
using SmartTalk.Messages.Enums.WeChat;
using TranscriptionFileType = SmartTalk.Messages.Enums.STT.TranscriptionFileType;
using TranscriptionResponseFormat = SmartTalk.Messages.Enums.STT.TranscriptionResponseFormat;

namespace SmartTalk.Core.Services.PhoneOrder;

public partial interface IPhoneOrderService
{
    Task<GetPhoneOrderRecordsResponse> GetPhoneOrderRecordsAsync(GetPhoneOrderRecordsRequest request, CancellationToken cancellationToken);

    Task ReceivePhoneOrderRecordAsync(ReceivePhoneOrderRecordCommand command, CancellationToken cancellationToken);

    Task ExtractPhoneOrderRecordAiMenuAsync(List<SpeechMaticsSpeakInfoDto> speakInfos, PhoneOrderRecord record, CancellationToken cancellationToken);
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
        
        // transcript auto
        var transcriptionBase = await _speechToTextService.SpeechToTextAsync(
            command.RecordContent, null, TranscriptionFileType.Wav, TranscriptionResponseFormat.Text, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        var detection = await _translationClient.DetectLanguageAsync(transcriptionBase, cancellationToken).ConfigureAwait(false);
        
        var record = new PhoneOrderRecord { SessionId = Guid.NewGuid().ToString(), Restaurant = recordInfo.Restaurant, CreatedDate = recordInfo.OrderDate.AddHours(-7), Status = PhoneOrderRecordStatus.Recieved };

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
        
        var transcriptionJobIdJObject = JObject.Parse(await CreateTranscriptionJobAsync(command.RecordContent, command.RecordName, cancellationToken).ConfigureAwait(false)) ;
        
        var transcriptionJobId = transcriptionJobIdJObject["id"]?.ToString();
        
        record.TranscriptionJobId = transcriptionJobId;
     
        Log.Information("Phone order record transcriptionJobId: {@transcriptionJobId}", transcriptionJobId);
        
        await AddPhoneOrderRecordAsync(record, PhoneOrderRecordStatus.Diarization, cancellationToken).ConfigureAwait(false);
    }

    public async Task ExtractPhoneOrderRecordAiMenuAsync(
        List<SpeechMaticsSpeakInfoDto> speakInfos, PhoneOrderRecord record, CancellationToken cancellationToken)
    {
        var (transcription, recordContent) = await TranscriptAsync(speakInfos, record, cancellationToken).ConfigureAwait(false);

        await SendWorkWeChatRobotNotifyAsync(recordContent, new PhoneOrderRecordInformationDto
        {
            OrderDate = record.CreatedDate,
            Restaurant = record.Restaurant,
            WorkWeChatRobotKey = record.Restaurant switch
            {
                PhoneOrderRestaurant.JiangNanChun => _phoneOrderSetting.GetSetting("江南春"),
                PhoneOrderRestaurant.XiangTanRenJia =>  _phoneOrderSetting.GetSetting("湘潭人家"),
                PhoneOrderRestaurant.MoonHouse => _phoneOrderSetting.GetSetting("福满楼"),
                _ => throw new Exception("Restaurant not exist")
            }
        }, transcription, cancellationToken).ConfigureAwait(false);
        
        // await ExtractAiOrderInformationAsync(transcription, record, cancellationToken).ConfigureAwait(false);
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
    
     public async Task<(string, byte[])> TranscriptAsync(List<SpeechMaticsSpeakInfoDto> phoneOrderInfo, PhoneOrderRecord record, CancellationToken cancellationToken)
    {
        var texts = new List<string>();
        
        var httpClient = new HttpClient();
        
        var localhostUrl = await httpClient.GetByteArrayAsync(record.Url, cancellationToken).ConfigureAwait(false);
            
        foreach (var speakDetail in phoneOrderInfo)
        {
            var speakStartTimeVideo = speakDetail.StartTime * 1000 - 0;
            var speakEndTimeVideo = speakDetail.EndTime * 1000 - 0;

            Log.Information("Start time of speak in video: {SpeakStartTimeVideo}, End time of speak in video: {SpeakEndTimeVideo}", speakStartTimeVideo, speakEndTimeVideo);

            try
            {
                var originText = await SplitAudioAsync(localhostUrl, record, speakStartTimeVideo, speakEndTimeVideo, TranscriptionFileType.Wav, cancellationToken).ConfigureAwait(false);

                Log.Information("Phone Order transcript originText: {originText}", originText);

                var intent = await RecognizeIntentAsync(originText, cancellationToken).ConfigureAwait(false);

                switch (intent)
                {
                    case PhoneOrderIntent.AddOrder:
                        var extractFoods = new PhoneOrderDetailDto();  //AddOrderDetailAsync
                        
                        var similarFoods = await GetSimilarRestaurantByRecordAsync(record, extractFoods, cancellationToken).ConfigureAwait(false);
                        break;
                    case PhoneOrderIntent.ReduceOrder:
                        //ReduceOrderDetailAsync
                        break;
                }
                
                // S1 餐厅，S2 客人
                texts.Add(speakDetail.Speaker + ":" + originText);
                
                await _phoneOrderDataProvider.AddPhoneOrderConversationsAsync().ConfigureAwait(false);
                
                await 
            }
            catch (Exception ex)
            {
                record.Status = PhoneOrderRecordStatus.Exception;

                Log.Information("transcription error: {ErrorMessage}", ex.Message);
            }
        }
        
        record.Status = PhoneOrderRecordStatus.Sent;
        record.TranscriptionText = string.Join(" ", texts);
        
        await _phoneOrderDataProvider.UpdatePhoneOrderRecordsAsync(record, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        return (record.TranscriptionText, localhostUrl);
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
            
            var payload = similarFoodsResponse.First().Item1.Payload.ToString();
            
            if (string.IsNullOrEmpty(payload)) return null;
            
            foodDetail.FoodName = JsonConvert.DeserializeObject<RestaurantPayloadDto>(payload).Name;
            
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

        // var reSplitAudios = await _ffmpegService.SplitAudioAsync(splitAudios.FirstOrDefault(), secondsPerAudio: 60 * 2, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        // var transcriptionBase = await _speechToTextService.SpeechToTextAsync(
        //     reSplitAudios[0], null, TranscriptionFileType.Wav, TranscriptionResponseFormat.Text, null, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        // var detection = await _translationClient.DetectLanguageAsync(transcriptionBase, cancellationToken).ConfigureAwait(false);

        // var (language, prompt) = SelectRestaurantMenuPrompt((record.Restaurant, detection.Language));
        
        var transcriptionResult = new StringBuilder();
        var conversations = new List<PhoneOrderConversation>();
        
        foreach (var reSplitAudio in splitAudios)
        {
            var transcriptionResponse = await _speechToTextService.SpeechToTextAsync(
                reSplitAudio, language, TranscriptionFileType.Wav, TranscriptionResponseFormat.Text, cancellationToken: cancellationToken).ConfigureAwait(false);
            conversations.Add();
            transcriptionResult.Append(transcriptionResponse);
        }

        await _phoneOrderDataProvider.AddPhoneOrderConversationsAsync(conversations, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        Log.Information("Transcription result {Transcription}", transcriptionResult.ToString());
        
        return transcriptionResult.ToString();
    }
    
    public async Task SendWorkWeChatRobotNotifyAsync(byte[] recordContent, PhoneOrderRecordInformationDto recordInfo, string transcription, CancellationToken cancellationToken)
    {
        await _weChatClient.SendWorkWechatRobotMessagesAsync(recordInfo.WorkWeChatRobotUrl,
            new SendWorkWechatGroupRobotMessageDto
            {
                MsgType = "text",
                Text = new SendWorkWechatGroupRobotTextDto
                {
                    Content = $"----------{recordInfo.Restaurant.GetDescription()}-PST {recordInfo.OrderDate.ToOffset(TimeSpan.FromHours(-7)):yyyy/MM/dd HH:mm:ss}----------"
                }
            }, cancellationToken);
        
        var splitAudios = await ConvertAndSplitAudioAsync(recordContent, secondsPerAudio: 60, cancellationToken: cancellationToken).ConfigureAwait(false);

        await SendMultiAudioMessagesAsync(splitAudios, recordInfo, cancellationToken).ConfigureAwait(false);

        await _weChatClient.SendWorkWechatRobotMessagesAsync(
            recordInfo.WorkWeChatRobotUrl, new SendWorkWechatGroupRobotMessageDto
            {
                MsgType = "text", Text = new SendWorkWechatGroupRobotTextDto { Content = transcription }
            }, CancellationToken.None);
        
        await _weChatClient.SendWorkWechatRobotMessagesAsync(
            recordInfo.WorkWeChatRobotUrl, new SendWorkWechatGroupRobotMessageDto
            {
                MsgType = "text", Text = new SendWorkWechatGroupRobotTextDto { Content = "-------------------------End-------------------------" }
            }, CancellationToken.None);
    }
    
    public async Task<List<byte[]>> ConvertAndSplitAudioAsync(byte[] record, int secondsPerAudio, CancellationToken cancellationToken)
    {
        var amrAudio = await _ffmpegService.ConvertWavToAmrAsync(record, "", cancellationToken: cancellationToken).ConfigureAwait(false);

        return await _ffmpegService.SplitAudioAsync(amrAudio, secondsPerAudio, "amr", cancellationToken: cancellationToken).ConfigureAwait(false);
    }
    
    public async Task SendMultiAudioMessagesAsync(List<byte[]> audios, PhoneOrderRecordInformationDto recordInfo, CancellationToken cancellationToken)
    {
        foreach (var audio in audios)
        {
            var uploadResponse = await _weChatClient.UploadWorkWechatTemporaryFileAsync(
                recordInfo.WorkWeChatRobotUploadVoiceUrl, Guid.NewGuid() + ".amr", UploadWorkWechatTemporaryFileType.Voice, audio, cancellationToken: cancellationToken).ConfigureAwait(false);
            
            if (string.IsNullOrEmpty(uploadResponse?.MediaId)) continue;
            
            await _weChatClient.SendWorkWechatRobotMessagesAsync(recordInfo.WorkWeChatRobotUrl,
                new SendWorkWechatGroupRobotMessageDto
                {
                    MsgType = "voice",
                    Voice = new SendWorkWechatGroupRobotFileDto { MediaId = uploadResponse.MediaId }
                }, cancellationToken);
        }
    }

    public async Task ExtractAiOrderInformationAsync(string transcription, PhoneOrderRecord record, CancellationToken cancellationToken)
    {
        if (!await DecideWhetherPlaceAnOrderAsync(transcription, cancellationToken).ConfigureAwait(false)) return;
        
        await ExtractPhoneOrderAiMenuAsync(transcription, record, cancellationToken).ConfigureAwait(false);
    }
    
     private async Task<PhoneOrderIntent> RecognizeIntentAsync(string input, CancellationToken cancellationToken)
    {
        var response = await _smartiesClient.PerformQueryAsync(new AskGptRequest
        {
            Messages = new List<CompletionsRequestMessageDto>
            {
                new ()
                {
                    Role = "system",
                    Content = new CompletionsStringContent("你是一个智能助手，能够根据用户的输入识别出相应的意图。以下是可能的意图和对应的样本：\n{\n  \"0\": {\"intent\": \"闲聊\", \"sample\": [\"你今日开心吗？\", \"今天天气点呀？\", \"你做紧d咩？\"]},\n  \"1\": {\"intent\": \"问菜品\", \"sample\": [\"今日有什麼可以食\", \"你们有什么推荐菜\", \"今日有咩推荐\", \"有什么粥吃\", \"有什么饭食\", \"小吃有什么\", \"有什么喝的？\", \"有什么饮料？\" ]},\n  \"2\": {\"intent\": \"打招呼\", \"sample\": [\"hello\", \"你好\", \"喂\", \"hi\"]},\n  \"3\": {\"intent\": \"转人工\", \"sample\": [\"帮我换人工\", \"人工服务\", \"转人工\", \"叫个真人嚟\", \"我需要人工客服。\"]},\n  \"4\": {\"intent\": \"下单\", \"sample\": [\"就呢啲啦\", \"可以了\", \"够啦，多謝\", \"帮我落单吧\", \"OK，就上面呢啲\"]},\n  \"5\": {\"intent\": \"营业时间\", \"sample\": [\"营业时间乜？\", \"你哋幾點开门？\", \"你哋幾點关门？\", \"宜家仲营业嗎？\", \"你哋今日几点开门？\", \"今天还营业吗？\", \"今天还营业吗？\"]},\n  \"6\": {\"intent\": \"地址\", \"sample\": [\"餐厅喺边度？\", \"地址有冇？\", \"地址喺边度？\", \"餐厅喺咩位置？\", \"可唔可以讲下餐厅嘅地址？\", \"餐厅在哪里？\"]},\n  \"7\": {\"intent\": \"是否gluten free\", \"sample\": [\"呢道菜系咪gluten free嘅？\", \"请问呢道菜系咪gluten free嘅？\", \"这道菜系gluten free的吗？\", \"这道菜有麸质吗？\", \"这道菜是gluten free的吗？\"]}\n  \"8\": {\"intent\": \"加单\", \"sample\": [\"帮我拿个白粥\", \"仲要两份椒盐鸡翼\", \"再嚟个扬州炒饭\", \"多個海鮮炒面同埋炸鸡翼\", \"我要叉烧炒饭\", \"\"]},\n  \"9\": {\"intent\": \"减单\", \"sample\": [\"你帮我去一个菠萝油,留一个给老婆\", \"唔要啱啱嗰道菜\", \"取消啱啱點嘅全部菜\", \"唔要魚旦啦\", \"乜都唔要啦\", \"不要刚刚点的了\", \"取消扬州炒饭\", \"取消扬州炒饭\"]},\n  \"10\": {\"intent\": \"问单\", \"sample\": [\"睇下我點咗啲乜嘢？\", \"落单裡有啲乜嘢？\", \"宜家落左啲乜嘢？\", \"睇下我宜家单里有啲乜嘢。\", \"现在下了些什么\", \"落了些什么\" ]},\n  \"11\": {\"intent\": \"欢送语\", \"sample\": [\"再见\", \"拜拜\", \"下次见\", \"冇咗，多谢\"]},\n  \"12\": {\"intent\": \"有无味精\", \"sample\": [\"菜里面有冇味精？\", \"会唔会放味精？\", \"呢度面有冇味精？\", \"请问呢道菜加咗味精未呀？\", \"有味精吗里面\"]},\n}\n根据以上意图和样本，请识别以下用户输入的意图，并返回相应的意图标识符（例如 1或2）。\n用户输入示例1：我想要一罐可乐，一份扬州炒饭，一份湿炒牛河，输出8\n用户输入示例2：有无咩推荐啊，输出1\n用户输入示例3：我落左咩单，全部讲来听下，输出10\n用户输入示例4：帮我落单，输出4\n用户输入示例5：落单之前的菜，输出4\n用户输入示例6：下单一份炒饭，输出8\n用户输入示例6：下单炒饭，输出8\n--规则：\n1. 如果打招呼intent和其他intent同时存在的时候，优先选择其他intent，例如\"hello，今日有什么卖啊？\"，应该识别成【问菜品】的intent而不是打招呼\n2. 如果下单intent中输入有菜品，都应该落入加单intent中而不是下单\n请根据用户的实际输入进行意图识别，并返回一个标识数字表示识别出的意图。")
                },
                new ()
                {
                    Role = "user",
                    Content = new CompletionsStringContent($"用户输入: {input}\n输出意图数字:")
                }
            },
            Model = OpenAiModel.Gpt4o
        }, cancellationToken).ConfigureAwait(false); 

        var responseContent = response.Data.Response;
        
        return int.TryParse(responseContent, out var intent) && Enum.IsDefined(typeof(PhoneOrderIntent), intent) ? (PhoneOrderIntent)intent : PhoneOrderIntent.Default;
    }

    public async Task ExtractPhoneOrderAiMenuAsync(string transcription, PhoneOrderRecord record, CancellationToken cancellationToken)
    {
        var messages = new List<CompletionsRequestMessageDto>
        {
            new ()
            {
                Role = "system",
                Content = new CompletionsStringContent("你是一款高度理解语言的智能助手，专门用于识别和处理电话订单。\n" +
                                                       "你需要根据电话录音的内容，判断客人通话中需要今天内出单给厨房配餐的菜单中的菜品名和数量以及单价\n" +
                                                       "注意用json的格式返回:规则: [{\"food_name\":\"菜品名\",\"quantity\":1,\"price\":0}]"+
                                                       "具体有以下规则:\n1.对话中有出现过菜品名、菜品价格、菜品数量等词语\n2.如果对话中没有出现菜品价格和菜品数量，则菜品价格为0菜品数量为1\n3.禁止将总价赋予某个菜品，总价一般是最后的价格\n4.严格按着规定格式返回，不要添加```json```\n5.不要提取重复的菜名或者相识的菜品\n6.提取出来的菜名翻译成中文\n" +
                                                       "- 样本与输出：\n" +
                                                       "input:你好 你好 我想要一份松花鱼 红烧松花鱼还是糖醋松花鱼 要红烧松花鱼 好还有需要的吗 上海青有吗 有上海青 多少钱 27 好再来两份小炒肉 青椒小炒肉还是黄牛小炒肉 青椒小炒肉 好359 大概要多少 三十五分钟 还要一个六块的香肠 好.output:[{\"food_name\":\"红烧松花鱼\",\"quantity\":1,\"price\":0},{\"food_name\":\"青椒小炒肉\",\"quantity\":2,\"price\":0},{\"food_name\":\"上海青\",\"quantity\":1,\"price\":27},{\"food_name\":\"香肠\",\"quantity\":1,\"price\":6}]\n" +
                                                       "input:你好我想要一份扬州炒饭 好的 它里面有什么 有火腿、香菇等 好的.output:[(\"food_name\":\"扬州炒饭\",\"quantity\":1,\"price\":0)]\n" +
                                                       "input:你好要一份海鲜烩饭 好的 海鲜烩饭中有什么海鲜 有虾仁,鲍鱼 不好意思我对这个两个过敏，不要了 好的，有其他需要吗 没有了.output:null\n" +
                                                       "input:你好,您好,我只是想知道如果我可以下单 可以,您的电话号码是什么? 559-765-6199 好的,您的下单是什么? 我可以来两个康康鸡丸特写吗? 好的,您想要什么? 它是有黄米的,对吗? 您想要黄米,没问题 您想要辣的味增汤吗? 辣的和酸辣的 是的,这个 好的,十分钟 好的,再见.output:[{\"food_name\":\"康康鸡丸特写\",\"quantity\":2,\"price\":0},{\"food_name\":\"味增汤\",\"quantity\":1,\"price\":0}]\n" +
                                                       "input:可以幫我點餐嗎? 310-826-9668 點什麼? 午餐特別,蒙古牛肉 好的 午餐特別,辣椒魚 好的 午餐特別,蝦肉配蘿蔔醬 好的 一個普通的蒙古牛肉 一個普通的蒙古牛肉? 是的 好的 一個六塊的香腸 好的 這樣就夠了嗎? 多少錢? 總共是九十多塊 我們會花20分鐘 是的 我們可以點兩碗甜酸湯和一碗玉米湯嗎? 好的 謝謝 再見.output:[{\"food_name\":\"蒙古牛肉\",\"quantity\":1,\"price\":0},{\"food_name\":\"辣椒魚\",\"quantity\":1,\"price\":0},{\"food_name\":\"蝦肉配蘿蔔醬\",\"quantity\":1,\"price\":0},{\"food_name\":\"香腸\",\"quantity\":1,\"price\":6},{\"food_name\":\"甜酸湯\",\"quantity\":2,\"price\":0},{\"food_name\":\"玉米湯\",\"quantity\":1,\"price\":0},]")
            },
            new ()
            {
                Role = "user",
                Content = new CompletionsStringContent($"input:\n\"{transcription}\",output:")
            }
        };

        var gptResponse = await _smartiesClient.PerformQueryAsync(new AskGptRequest
        {
            Model = OpenAiModel.Gpt4o,
            Messages = messages.Select(m => new CompletionsRequestMessageDto
            {
                Content = m.Content,
                Role = m.Role.ToString().ToLower()
            }).ToList()
        }, cancellationToken).ConfigureAwait(false);

        Log.Information("Transcription: {transcription},\n Menu result: {result}", transcription, gptResponse.Data.Response);

        if (gptResponse.Data.Response == null) return;
        
        var dishes = JsonConvert.DeserializeObject<List<PhoneOrderOrderItemDto>>(gptResponse.Data.Response);

        dishes = dishes.Select(x =>
        {
            x.RecordId = record.Id;
            return x;
        }).ToList();
        
        await _phoneOrderDataProvider.AddPhoneOrderItemAsync(_mapper.Map<List<PhoneOrderOrderItem>>(dishes), cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> DecideWhetherPlaceAnOrderAsync(string transcription, CancellationToken cancellationToken)
    {
        var messages = new List<CompletionsRequestMessageDto>
        {
            new ()
            {
                Role = "system",
                Content = new CompletionsStringContent("你是一个精通餐厅话术的客服经理,专门分析电话录音中是否有下单动作。\n" +
                                                       "你需要根据电话录音的内容，判断客人通话的目的是否为【叫外卖】、【预订餐】等需要今天内出单给厨房配餐\n" +
                                                       "注意返回格式: true or false\n"+
                                                       "具体有以下特征：\n正面特征:\n1.对话中有出现过菜品名、菜品价格、菜品数量、电话号码、总价格等词语\n2.有提及需要多久\n反面特征:\n1.没有出现菜品名、菜品价格、菜品数量、电话号码、总价格等词语\n2.出现预定、询问订单的情况" +
                                                       "反面例子：input:\"你好 你好 星期五可以预定吗 可以 你需要什么 有什么套餐吗 有宴会套餐a1488和宴会套餐b1688 我需要宴会套餐a 好的 你的手机号码 xxx-xxx-xxx 好的.\"output:false\n" +
                                                       "正面例子：input:\"你好 你好 我想要一份松花鱼 好还需要什么 上海青有吗 有 好再来两份小炒肉 好的 大概什么时候好 二十分钟之后 好.\"output:true。")
            },
            new ()
            {
                Role = "user",
                Content = new CompletionsStringContent($"input:\n\"{transcription}\",output:")
            }
        };

        var gptResponse = await _smartiesClient.PerformQueryAsync(new AskGptRequest
        {
            Model = OpenAiModel.Gpt4o,
            Messages = messages.Select(m => new CompletionsRequestMessageDto
            {
                Content = m.Content,
                Role = m.Role.ToString().ToLower()
            }).ToList()
        }, cancellationToken).ConfigureAwait(false);

        Log.Information("Transcription: {transcription},\n result: {result}", transcription, gptResponse.Data.Response);

        return bool.Parse(gptResponse.Data.Response);
    }
    
    private (TranscriptionLanguage, string) SelectRestaurantMenuPrompt((PhoneOrderRestaurant, string)  restaurant)
    {
        return restaurant switch
        {
            (PhoneOrderRestaurant.MoonHouse, "en") => (TranscriptionLanguage.English, "Beef Pan Fried Noodle,Chicken Pan Fried Noodle,BBQ Pork Pan Fried Noodle,Vegetable Pan Fried Noodle,Shrimp Pan Fried Noodle,House Special Pan Fried Noodle,Seafood Pan Fried Noodle,Dry Style Shrimp Chow Fun,Dry Style House Special Chow Fun,Dry Style Seafood Chow Fun,Dry Style BBQ Pork Chow Fun,Dry Style Vegetable Chow Fun,Dry Style Fish Chow Fun,Beef Chow Fun w.Spicy Garlic & Black Bean Sauce,Wet Style Beef chow Fun,Paradise Desire,Har Gow,Siu Mai,Char siu Bao,BBQ Pork,BBQ Spare Ribs,Cream Cheese Wonton,Shanghai Xiao Log Bao,Pan Fried Dumpling,Steamed Dumpling,Egg Rolls,Chicken Salad,Sizzling Rice Soup,Wonton Soup,Chicken Corn Soup,Hot & Sour Soup,Vegetable Soup,Seafood Tofu Soup,Westlake Style Minced Beef Soup,Crab Meat and Fish Maw Soup,Egg Drop Soup,Steamed Filet of sole,Filet Mignon French Style,Zi Ran Lamb,Salt and Pepper Fish,Peking Duck,Hot Pot Lamb Stew,Hot Pot Seafood Combination,Cantonese Beef Stew Hot Pot,BBQ Roast Duck,Beef w.Broccoli,Mongolian Beef,Beef with Snow Peas Sauce, Beef with Black Bean Sauce, Beef with Asparaqus,Mushroom Beef,Vegetable Beef,Orange Beef,Kung Pao Beef,Kung Pao Chicken,Cashew Chicken,Garden Chicken,Orange Chicken,Sweet and Sour Chicken,Garlic Chicken,Moo Goo Gai Pan,Black Pepper Chicken,Lemon Chicken,Sweet and Pungent Chicken,Curry Chicken,Chicken with Asparagus,Chicken Lettuce Wraps,Sweet & Sour Chicken,Broccoli Chicken,Mongolia Chicken,Snow Pea Chicken,Black Bean Sauce Chicken,Garlic Shrimp,Shrimp with Lobster Sauce,Garden Shrimp,Shrimp with Black Bean Sauce,Shrimp with Asparagus,Sweet and Pungnet Shrimp,Fish with Black Bean Sauce,Szechuan Scallop,Salt and Pepper Scallop,Salt and Pepper Calamari,Crispy Walnut Shrimp,Lemon Scallop,Salt & Pepper Shrimp,Garden Fish,Kung Pao Scallop,Kung Pao Calamari,Snow Pea Shrimp,Steam Tilapia,Kung Pao Shrimp,Shrimp W.Scrambled egg,Sweet & Sour Shrimp,Sweet & Sour Fish Fillet,Ginger & Scallion Squid,Curry Shrimp,Broccoli Shrimp,Broccoli Fish Fillet,Fresh Squid W.Black Bean Sauce,Braised Tofu,Sweet and Sour Pork,BBQ Pork with Snow Peas,Pork Belly with Pickle Vegetable,Szechuan Pork,Pork Chop with Peking Sauce,Pork Chop with Spicy Saly,Ong-Choy,Snow Pea Leaves,Mixed Vegetables,Mapo Tofu,Hot Braised String Beans,Hot Szechuan Eggplant,Sauteed Spinach,Baby Bok Choy with Mushrooms,Broccoli W.Garlic,Mushroom & Spinach,Garlc Chinese Broccoli,Broccoli Oysters Sauce,Moo Shu Vegetable,Moo Shu Pork,Moo Shu Shrimp,Plain Egg Foo Young,Chicken Egg Foo Young,Shrimp Egg Foo Young,Moo Shu Chicken,Moo Shu Beef,Vegetable Fried Rice,Chicken Fried Rice,BBQ Pork Fried Rice,Shrimp Fried Rice,Yang Chow Frie Rice,House Special Fried Rice,Salted Fish and Diced Chicken Fried Rice,Seafood Egg White Fried Rice,Beef Fried Rie,Egg Fried Rice,Hong Kong Roast Duck on Steamed Rice,Hong Kong BBQ Pork on Steamed Rice,Minced Beef on Steamed Rice,Fu Zhou Style Rice,Vgetable Chow Mein,Chicken Chow Mein,BBQ Pork Chow Mein,Dry Style Beef/Chicken Chow Fun,Chicken Chow Fun,Chow Mein Chinatown Style,Spicy Singapore Style Rice Noodles,Seafood Chow Mein,House Special Chow Mein,Shrimp Chow Mein,Beef Chow Mein,Wonton Noodles in Soup,Sliced Chicken Noodles in Soup,Spicy Beef Noodles in Soup,BBQ Pork Nooles in Soup,Wor Wonton Noodles in Soup,Roast Duck Noodles in Soup,Seafood Noodles in Soup,Seafood Porridge,Mince Beef Porridge,Pork with Preserve Egg Porridge,Plain Porridge,Chinese Donut,White Rice,Brown Rice,Chicken Porridge,Fish Porridge,Lemonade,Perrier,Iced Tea,Thai Ice Tea,Soda,Bottled Water,Cake of the day,Wong Lo Kat,Char Siu Bao,Siu Mai,Har Gow,Cream Cheese Wonton,Steam Dumplings,Pan Fries Dumplings,Egg Rolls,Shanghai Xiao Long Bao,Vegetable Party Tray,Chicken Party Tray,Beef Party Tray,Shrimp Party Tray,Frice Rice Party Tray,Noodles Party Tray" + "Zi Ran Lamb ,Stir-Fried Chicken,Stir-Fried Sliced Chicken,Sliced Chicken with Curry, Szechuan Kung Pao Chicken,Orange Peel Flavor Chicken,Chicken with Szechuan Garlic Sauce,Asparagus Chicken,Chicken with Mixed Vegetables,Mongolian Beef,Sliced Beef Stir-Fried,Sliced Beef with Mushrooms,Sliced Beef with Broccoli , Baked Pork Chop with Peking Sauce,Pork Chop w/Garlic & Spicy Salt,Shredded Pork,Sweet and Sour Pork ,Sauteed Spinach with Mushrooms,Sauteed Nappa with XO Sauce ,Gai Lan with Oyster Sauce,Hot Braised String Beans ,Eggplant in Garlic Sauce ,Braised Tofu with Black Mushrooms,Mapo Tofu,Mixed Vegetable Deluxe,Steamed Tilapia Fish,Fish Fillet with Wok Tossed Vegetables ,Fish Fillet with Black Bean and Chili Sauce,Sweet and Sour Fish Fillet ,Stir-Fried Squid Lunch Special,Squid with Ginger and Green Onions Lunch Special ,Squid with Asparagus Lunch Special,Shrimp with Fresh Mixed Vegetables Lunch Special,Black Bean and Chili Sauteed Shrimp Lunch Special ,Shrimp with Snow Peas Lunch Special,Szechuan Kung Pao Shrimp Lunch Special,Broccoli Shrimp Lunch Special,Shrimp with Lobster Sauce Lunch Special ,Szechuan Spicy Garlic Shrimp Lunch Special,Shrimp with Asparagus Lunch Special,Honey Glazed Walnut Shrimp Lunch Special,Chef's Special Beef Fillet Lunch Special,Sauteed 3 Ingredients on Tofu Lunch Special,Pork Belly W.Pickle Vegetable,Sweet & Sour Chicke,Broccoli Chicken,Mixed Vegetable"),
            (PhoneOrderRestaurant.JiangNanChun, "en") => (TranscriptionLanguage.English, "Avocado with Preserved Egg&Tofu,Crystal Ham,Salted Duck,Shanghai Shimmering Shrimp,Salt Roast Chicken,Smoked Carp,Wine-infused shrimp,Wine-infusd Chicken,Chilli Garlic Pork Belly Slice,Pickled Radish,Spring Roll,Hot & Sour Soup w/Fried Dough,Clam Soup Tofu Soup,West Lake Beef Soup,Madam Fish Soup,Wonton Soup,Bamboo Shoot Soup w/Pickled Pork,Lamb w/Sour Cabbage,Tomato Spare Rib Soup,Sizzling Rice Soup,Chicken Corn Soup,Clam & Melon Soup,Jun's Chicken Soup(bood in advance),Jun's Duck Soup(bood in advance),Yellow Croaker,Sweet & sour Crispy,Braised,Pickled Mustard,Three-Cup cod,Sizzled Eel,Homestyle White Pomfret with Rice Cake,Steamed Yellow Croaker with Clam,Seaweed Fried Fish,Stir Fry Shrimp,Basil w/Clams,Abalone Rice Cake,Golden Pickle Cabbage Fish,Fish Fillet w/Hot Bean,Sweet & Sour Fish Fillet,Minced Shrimp w/Lettuce Wrap,Walnut Shrimp,Scallion Braised Sea Cucumber,Filet Mignon w/Lily Bulb,Supreme Meatball,Fried Garlic Ribs,Braised Pork(Abalone),Sichuan Twice Cook Pork,Truffle Lily Ribss,Golden Soup Beef,Beef Stir Fry,Broccoli,Asparagus,Snow Pea,Green Oion,Stir Fry Lamb w/Green Onion,Sweet & Sour Ribs,MooShu Pork,Stir Fry Shreded Pork w/Bean Curd,Stry Fry Pork w/Bamboo Shoots,Shanghai Poached Chicken(Half),Chili Pepper Fried Chicken,Tea Smoked Duck(Half),Kung Pao Chicken,Orange Chicken,Stir-fried Gound Pork with Chinese Chives,Chongqing Assorted Stew,Sichuan Boiled Fish with Pork Intestines,Sichuan Boiled Beef,Dry Pot Beef,Dry Pot Intestines,Dry Pot Cauliflower,Dry Pot Cabbage,Braised Pork Hock(bood in advance),Quinoa Spicy tofu w/Sea Cucumber,Hot Braised Tofu w/Shrimp,Three Ingredients w/Crispy Tofu,Iberian Ham Draped Asparagus and Lily,Quinoa and Yam Broth w/Golden Broth,Dried Fish Draped Loofah,Garden Stir Fry,Asparagus w/Bamboo Fungus,Pickled Mustard w/Green Pea,Salted Egg Yolk Bitter Melon,Saute String Bean w/Ground Pork,Sichuan Eggplant w/Ground Pork,Pickled Mustard with Edamame and Tofu Sheet,Stir Fry Vegetable,Black Truffle Abalone Fried Rice,Shanghai Thick Chow Mein,Shanghai Sausage Fried Rice,Fried Rice,Chicken,Beef,Shrimp,Pork,Combo,ChowMein,combo,Leek Rice Cake,Shanghai Rice Cake,Dumpling,Cabbage&Pork,Chive&Pork,Soup Dumpling,Beef Roll,Scallion Pancake,Rice,White Rice,Brown Rice,Mango Pomelo w/Sago,Sweet Rice Soup,Red Bean Pancake"),
            (PhoneOrderRestaurant.XiangTanRenJia, "en") => (TranscriptionLanguage.English, ""),
            (PhoneOrderRestaurant.MoonHouse, "zh-CH" or "zh-TW") => (TranscriptionLanguage.Chinese, "牛肉煎麵, 雞肉煎麵,叉燒煎麵,素菜煎麵,蝦仁煎麵,招牌煎麵,海鮮煎麺,乾炒蝦河,乾炒招牌河,乾炒海鮮河,乾炒叉燒河,乾炒素菜河,乾炒魚河,豉椒牛肉炒河粉,濕炒牛河,點心拼盤,晶瑩蝦餃,香菇燒麥,龍蒸叉燒包,蜜汁叉燒,五香燒排骨,芝士炸雲吞,上海小籠包,生煎鍋貼,生蒸餃子,上素春卷,雞絲沙拉,三鮮鍋巴湯,窩雲吞湯,雞茸玉米羹,川式酸辣湯,豆腐雜菜湯,海鮮豆腐湯,西湖牛肉羹,蟹肉魚肚羹,蛋花湯,清蒸魚柳,法式牛柳粒,孜然炒羊肉,椒鹽魚片,北京皮鴨,枝竹羊腩煲,海鮮豆腐煲,蘿蔔牛腩煲,港式燒鴨,西蘭花牛肉,蒙古牛肉,雪豆炒牛肉,豉椒炒牛肉,蘆筍炒牛肉,蘑菇牛肉,素菜牛,陳皮牛,宮保牛,宮保鷄片,腰果鷄片,鷄片素菜,陳皮雞,菠蘿甜酸雞,魚香鷄片,蘑菇鷄片,黑椒鷄片,檸檬汁鷄片,溜炒鷄片,咖喱鷄片,蘆筍鷄片,雞鬆生菜包,咕嘮鷄,西蘭花雞,蒙古雞,雪豆雞,豉椒雞,魚香蝦仁,鮮蝦龍糊,蝦仁素雜菜,豉椒炒蝦仁,蘆筍蝦仁,溜炒蝦仁,豉椒炒魚片,魚香帶子,椒鹽帶子,椒鹽鮮魷,核桃西汁蝦仁,檸檬汁帶子,椒鹽蝦,時菜魚片,宮保帶子,宮保鮮魷,雪豆蝦,蒸立漁,宮保蝦,蝦仁炒蛋,甜酸蝦,甜酸魚片,薑葱鮮魷,咖喱蝦,西蘭花蝦,西蘭花魚柳,豉椒鮮魷,紅燒豆腐,菠蘿甜酸肉,叉燒炒雪豆,梅菜扣肉,魚香肉絲,京都汁豬扒,椒鹽豬扒,腐乳通心菜,蒜蓉炒豆苗,清炒雜菜,麻婆豆腐,乾煸四季豆,魚香茄子,蒜蓉炒菠菜,冬菇扒時菜,蒜蓉芥蘭,冬菇菠菜,蒜蓉西蘭花,耗油芥蘭,木須菜,木須肉,木須蝦,煎芙蓉蛋,鷄片芙蓉蛋,蝦芙蓉蛋,木須雞,木須牛,素菜炒飯,雞粒炒飯,叉燒炒飯,蝦仁炒飯,揚州炒飯,招牌炒飯,鹹魚雞粒炒飯,蛋白海鮮炒飯,牛肉炒飯,蛋炒飯,港式燒鴨飯,港式叉燒飯,免治牛肉飯,福州燴飯,素菜炒粗麵,鷄片炒粗麵,叉燒炒粗麵,乾炒牛/雞河,豉椒雞炒河,豉油皇炒麵,星洲炒米粉,海鮮炒粗麵,雞蝦牛炒粗麵,蝦仁炒面,牛肉炒粗麵,雲吞湯麵,鷄片湯麵,四川辣牛湯麵,叉燒湯麵,窩雲吞湯麵,燒鴨湯麵,海鮮湯麵,生熟海鮮粥,免治牛肉粥,皮蛋瘦肉粥,白粥,油條,白米飯,糙米,鷄片粥,魚片粥,檸檬汁,巴黎氣泡水,冰紅茶,泰式冰茶,汽水,瓶裝水,當日蛋糕,王老吉,叉燒包派對餐,香菇燒賣派對餐,蝦餃派對餐,芝士炸雲吞派對餐,生蒸餃子派對餐,生煎鍋貼派對餐,上素春卷派對餐,上海小籠包派對餐,素菜派對餐,雞派對餐,牛派對餐,蝦派對餐,炒飯派對餐,麵派對餐" + "孜然炒羊肉（午餐）,豉椒雞片（午餐）,蘑菇雞片（午餐）,咖哩雞片（午餐）,宮保雞片（午餐）,陳皮雞（午餐）,魚香雞片（午餐）,蘆筍雞片（午餐）,素雜菜雞片（午餐）,蒙古牛肉（午餐）,豉椒炒牛肉（午餐）, 蘑菇牛肉（午餐）,西蘭花牛肉（午餐）,京都汁肉扒（午餐）,椒鹽豬扒（午餐）,魚香肉絲（午餐）,菠蘿甜酸肉（午餐）,冬菇扒菠菜（午餐）,XO醬大白菜（午餐）,蠔油芥蘭（午餐）,乾煸四季豆（午餐）,魚香茄子（午餐）,紅燒豆腐（午餐）,麻婆豆腐（午餐）,羅漢齋（午餐）,清蒸立魚（午餐）,時菜炒魚片（午餐）,豉椒炒魚片（午餐）,菠蘿甜酸魚片（午餐）,豉椒炒鮮魷（午餐）,姜蔥炒鮮魷（午餐）,蘆筍鮮魷（午餐）,蝦仁素雜菜（午餐）,豉椒炒蝦仁（午餐）,雪豆炒蝦仁（午餐）,川式宮保蝦（午餐）,西蘭花蝦仁（午餐）,鮮蝦龍糊（午餐）,魚香蝦仁（午餐）,蘆筍蝦仁（午餐）,核桃西汁蝦仁（午餐）,法式牛柳粒（午餐）,三鮮扒豆腐（午餐）,梅菜扣肉（午餐）,咕嘮鷄（午餐）,西蘭花鷄（午餐）,炒什菜（午餐)"),
            (PhoneOrderRestaurant.JiangNanChun, "zh-CH" or "zh-TW") => (TranscriptionLanguage.Chinese, "皮蛋豆腐牛油果,肴肉,盐水鸭,油爆虾,咸香鸡,熏鱼,糟虾,醉鸡,蒜泥白肉,酱萝卜,素春卷,酸辣汤配油条,荠菜蛤肉白玉羹,西湖牛肉羹,宋嫂鱼羹,窝馄饨汤,腌笃鲜,酸菜羊肉汤,番茄小笋排骨湯,三鲜锅巴汤,鸡茸玉米汤,蛤蜊冬瓜汤,当归竹荪土鸡汤,火腿笋干老鸭汤,大黄鱼,松鼠,干烧,雪菜蒸,三杯鳕鱼,响油鳝糊,白鲳烧年糕,黃魚蒸蛤肉,苔条魚,清炒虾仁,九层塔蛤蝲,鲍鱼仔年糕,黄金酸菜鱼,豆瓣鱼片,糖醋鱼片,生菜虾松,核桃虾仁,葱烧乌参,鲜百合牛仔粒,霸王狮子头,蒜香骨,家常红烧肉,姬松茸回锅肉,松露百合糯米骨,金汤雪花牛肉,小炒牛肉,西兰花,芦笋,雪豆,葱爆,葱爆羊肉,糖醋小排,木须肉,香干炒肉丝,笋尖肉丝,上海白斩鸡,辣子鸡,樟茶鸭,宫保鸡,陈皮鸡,苍蝇头,毛血旺,水煮鱼+肥肠,水煮牛肉,干锅牛肉,干锅肥肠,干锅有机花菜,干锅手撕包菜,红烧蹄膀,藜麦海参麻辣豆腐,红烧山水豆腐,三鲜烩脆皮豆腐,芦笋鲜百合佐利比亚火腿,金汤藜麦煮山药,开洋丝瓜,薏米田园小炒,上汤竹荪浸芦笋,雪菜开洋烧青豆瓣,咸蛋黄苦瓜,干煸四季豆,鱼香茄子,雪菜毛豆百叶,清炒时令蔬,黑松露鲍鱼炒饭,上海粗炒面,香肠咸肉菜饭,炒饭,鸡肉,牛肉,虾仁,肉丝,揚州炒飯,炒面,三鲜,荠菜肉丝炒年糕,上海年糕,水饺,白菜猪肉,韭菜猪肉,上海小笼包,牛肉卷饼,葱油饼,饭,白饭,糙米饭,杨枝甘露,酒酿小圆子,豆沙锅饼"),
            (PhoneOrderRestaurant.XiangTanRenJia, "zh-CH" or "zh-TW") => (TranscriptionLanguage.Chinese,""),
            _ => (TranscriptionLanguage.Chinese, null)
        };
    }
    
    private async Task<string> CreateTranscriptionJobAsync(byte[] data, string fileName, CancellationToken cancellationToken)
    {
        var createTranscriptionDto = new SpeechMaticsCreateTranscriptionDto { Data = data, FileName = fileName };
        
        var jobConfigDto = new SpeechMaticsJobConfigDto
        {
            Type = SpeechMaticsJobType.Transcription,
            TranscriptionConfig = new SpeechMaticsTranscriptionConfigDto
            {
                Language = SpeechMaticsLanguageType.Auto,
                Diarization = SpeechMaticsDiarizationType.Speaker,
                OperatingPoint = SpeechMaticsOperatingPointType.Enhanced
            },
            NotificationConfig = new List<SpeechMaticsNotificationConfigDto>
            {
                new SpeechMaticsNotificationConfigDto{
                    AuthHeaders = _transcriptionCallbackSetting.AuthHeaders,
                    Contents = ["transcript"],
                    Url = _transcriptionCallbackSetting.Url
                }
            }
        };
        
        return await _speechMaticsClient.CreateJobAsync(new SpeechMaticsCreateJobRequestDto { JobConfig = jobConfigDto }, createTranscriptionDto, cancellationToken).ConfigureAwait(false);
        
    }
}