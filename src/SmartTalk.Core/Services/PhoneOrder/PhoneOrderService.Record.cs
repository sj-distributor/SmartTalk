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

        _backgroundJobClient.Enqueue(() => ExtractAiOrderInformationAsync(transcription, orderRecord[0].Id, false, cancellationToken));
        
        if (!string.IsNullOrEmpty(recordInfo.WorkWeChatRobotUrl))
            await SendWorkWeChatRobotNotifyAsync(command.RecordContent, recordInfo, transcription, cancellationToken).ConfigureAwait(false);
    }
    
    public async Task ExtractAiOrderInformationAsync(string transcription, int recordId, bool isUseGpt4, CancellationToken cancellationToken)
    {
        if (!await DecideWhetherPlaceAnOrderAsync(transcription, false, cancellationToken).ConfigureAwait(false)) return;
        
        await ExtractMenuAsync(transcription, false, recordId, cancellationToken).ConfigureAwait(false);
    }

    public async Task ExtractMenuAsync(string transcription, bool isUseGpt4, int recordId, CancellationToken cancellationToken)
    {
        var messages = new List<CompletionsRequestMessageDto>
        {
            new ()
            {
                Role = "system",
                Content = new CompletionsStringContent("\"You are a highly skilled AI capable of understanding and extracting dish information from text. Your task is to analyze the following article and find out the name of the dish, corresponding quantity, and price. Translate all dish names into traditional Chinese. Do not repeat the name of the dish. If the quantity is not mentioned, assume it is one. If no price is provided, the price is assumed to be zero. Ignore any total price or total amount that relates to the entire order rather than individual items. For example, if the article mentions \"Barbecue pork chow fun, spicy pork chops, total $44.90,\" you should extract:\n[{\"food_name\": \"Char Siu Fried noodles\", \"quantity\": 1, \"price\": 0},\n{\"food_name\": \"Spicy pork chop\", \"quantity\": 1, \"price\": 0}]")
            },
            new ()
            {
                Role = "user",
                Content = new CompletionsStringContent($" minutes:\n\"{transcription}\"")
            }
        };

        var gptResponse = await _smartiesClient.PerformQueryAsync(new AskGptRequest
        {
            Model = isUseGpt4 ? OpenAiModel.Gpt4o : OpenAiModel.Gpt35Turbo16K,
            Messages = messages.Select(m => new CompletionsRequestMessageDto
            {
                Content = m.Content,
                Role = m.Role.ToString().ToLower()
            }).ToList()
        }, cancellationToken).ConfigureAwait(false);

        Log.Information("Transcription: {transcription},\n Menu result: {result}", transcription, gptResponse.Data.Response);
        
        var dishes = JsonConvert.DeserializeObject<List<PhoneOrderOrderItem>>(gptResponse.Data.Response);

        dishes = dishes.Select(x =>
        {
            x.RecordId = recordId;
            return x;
        }).ToList();
        
        await _phoneOrderDataProvider.AddPhoneOrderItemAsync(dishes, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> DecideWhetherPlaceAnOrderAsync(string transcription, bool isUseGpt4, CancellationToken cancellationToken)
    {
        var messages = new List<CompletionsRequestMessageDto>
        {
            new ()
            {
                Role = "system",
                Content = new CompletionsStringContent("You are a highly skilled AI trained to understand language and determine if there is a need to place an order. Your task is to read the following article, analyze whether there is a demand for an order, and clearly determine whether the order is a takeaway (for example, if it mentions keywords such as \"takeaway\", \"delivery\", \"how long\", etc.) rather than a reservation. The purpose is to ensure that if there is an order demand and it is a takeaway, the order is placed successfully. If the article indicates a need for takeout, return yes, if not, return no.Use the following template to generate traditional Chinese:result: yes or no")
            },
            new ()
            {
                Role = "user",
                Content = new CompletionsStringContent($" minutes:\n\"{transcription}\"\nresult:\n")
            }
        };

        var gptResponse = await _smartiesClient.PerformQueryAsync(new AskGptRequest
        {
            Model = isUseGpt4 ? OpenAiModel.Gpt4o : OpenAiModel.Gpt35Turbo16K,
            Messages = messages.Select(m => new CompletionsRequestMessageDto
            {
                Content = m.Content,
                Role = m.Role.ToString().ToLower()
            }).ToList()
        }, cancellationToken).ConfigureAwait(false);

        Log.Information("Transcription: {transcription},\n result: {result}", transcription, gptResponse.Data.Response);

        return gptResponse.Data.Response switch
        {
            "result: no" => false,
            "result: yes" => true,
            _ => false
        };
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
}