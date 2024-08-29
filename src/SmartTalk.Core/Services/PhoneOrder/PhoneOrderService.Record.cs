using Serilog;
using Newtonsoft.Json;
using SmartTalk.Core.Extensions;
using Smarties.Messages.DTO.OpenAi;
using SmartTalk.Messages.Dto.WeChat;
using System.Text.RegularExpressions;
using Smarties.Messages.Enums.OpenAi;
using Smarties.Messages.Requests.Ask;
using SmartTalk.Messages.Enums.WeChat;
using SmartTalk.Core.Domain.PhoneOrder;
using SmartTalk.Messages.Dto.PhoneOrder;
using SmartTalk.Messages.Dto.Attachments;
using SmartTalk.Messages.Enums.PhoneOrder;
using SmartTalk.Messages.Commands.PhoneOrder;
using SmartTalk.Messages.Requests.PhoneOrder;
using SmartTalk.Messages.Commands.Attachments;
using TranscriptionFileType = SmartTalk.Messages.Enums.STT.TranscriptionFileType;
using TranscriptionLanguage = SmartTalk.Messages.Enums.STT.TranscriptionLanguage;
using TranscriptionResponseFormat = SmartTalk.Messages.Enums.STT.TranscriptionResponseFormat;

namespace SmartTalk.Core.Services.PhoneOrder;

public partial interface IPhoneOrderService
{
    Task<GetPhoneOrderRecordsResponse> GetPhoneOrderRecordsAsync(GetPhoneOrderRecordsRequest request, CancellationToken cancellationToken);

    Task ReceivePhoneOrderRecordAsync(ReceivePhoneOrderRecordCommand command, CancellationToken cancellationToken);
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
        if (await CheckPhoneOrderRecordDurationAsync(command.RecordContent, cancellationToken).ConfigureAwait(false)) return;
        
        var fileUrl = await UploadRecordFileAsync(command.RecordName, command.RecordContent, cancellationToken).ConfigureAwait(false);
        
        Log.Information($"Phone order record file url: {fileUrl}", fileUrl);
        
        if (string.IsNullOrEmpty(fileUrl)) return;

        var recordInfo = ExtractPhoneOrderRecordInfoFromRecordName(command.RecordName);
        
        Log.Information("Phone order record information: {@recordInfo}", recordInfo);
        
        var transcription = await _speechToTextService.SpeechToTextAsync(
            command.RecordContent, TranscriptionLanguage.Chinese, TranscriptionFileType.Wav, TranscriptionResponseFormat.Text, cancellationToken).ConfigureAwait(false);

        Log.Information("Phone order record transcription: " + transcription);
        
        // todo recognize speaker

        var orderRecord = new List<PhoneOrderRecord>
        {
            new() { SessionId = Guid.NewGuid().ToString(), Restaurant = recordInfo.Restaurant, TranscriptionText = transcription, Url = fileUrl }
        }; 
        
        await _phoneOrderDataProvider.AddPhoneOrderRecordsAsync(orderRecord, cancellationToken: cancellationToken).ConfigureAwait(false);

        _backgroundJobClient.Enqueue(() => ExtractAiOrderInformationAsync(transcription, orderRecord[0].Id, cancellationToken));
        
        if (!string.IsNullOrEmpty(recordInfo.WorkWeChatRobotUrl))
            await SendWorkWeChatRobotNotifyAsync(command.RecordContent, recordInfo, transcription, cancellationToken).ConfigureAwait(false);
    }
    
    public async Task ExtractAiOrderInformationAsync(string transcription, int recordId, CancellationToken cancellationToken)
    {
        if (!await DecideWhetherPlaceAnOrderAsync(transcription, cancellationToken).ConfigureAwait(false)) return;
        
        await ExtractMenuAsync(transcription, recordId, cancellationToken).ConfigureAwait(false);
    }

    public async Task ExtractMenuAsync(string transcription, int recordId, CancellationToken cancellationToken)
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
                                                       "input:你好要一份海鲜烩饭 好的 海鲜烩饭中有什么海鲜 有虾仁,鲍鱼 不好意思我对这个两个过敏，不要了 好的，有其他需要吗 没有了.output:null")
            },
            new ()
            {
                Role = "user",
                Content = new CompletionsStringContent($" minutes:\n\"{transcription}\"")
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
            x.RecordId = recordId;
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
                                                       "反面例子：\"你好 你好 星期五可以预定吗 可以 你需要什么 有什么套餐吗 有宴会套餐a1488和宴会套餐b1688 我需要宴会套餐a 好的 你的手机号码 xxx-xxx-xxx 好的.\" false;\n" +
                                                       "正面例子：\"你好 你好 我想要一份松花鱼 好还需要什么 上海青有吗 有 好再来两份小炒肉 好的 大概什么时候好 二十分钟之后 好.\"true。")
            },
            new ()
            {
                Role = "user",
                Content = new CompletionsStringContent($"article:\n\"{transcription}\"\nresult:")
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

    private async Task<bool> CheckPhoneOrderRecordDurationAsync(byte[] recordContent, CancellationToken cancellationToken)
    {
        var audioDuration = await _ffmpegService.GetAudioDurationAsync(recordContent, cancellationToken).ConfigureAwait(false);

        Log.Information($"Phone order record audio duration: {audioDuration}", audioDuration);

        var timeSpan = TimeSpan.Parse(audioDuration);

        return timeSpan.TotalSeconds < 3 || timeSpan.Seconds == 14;
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
            OrderNumber = phoneNumber,
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
    
    private async Task UpdatePhoneOrderRecordSpecificFieldsAsync(int recordId, int modifiedBy, string tips, CancellationToken cancellationToken)
    {
        var record = await _phoneOrderDataProvider.GetPhoneOrderRecordByIdAsync(recordId, cancellationToken).ConfigureAwait(false);

        record.Tips = tips;
        record.LastModifiedBy = modifiedBy;

        await _phoneOrderDataProvider.UpdatePhoneOrderRecordsAsync(record, cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}