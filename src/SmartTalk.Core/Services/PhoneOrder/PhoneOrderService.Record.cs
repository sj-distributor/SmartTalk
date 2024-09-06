using Serilog;
using SmartTalk.Core.Extensions;
using SmartTalk.Messages.Enums.STT;
using SmartTalk.Messages.Dto.WeChat;
using System.Text.RegularExpressions;
using SmartTalk.Messages.Enums.WeChat;
using SmartTalk.Core.Domain.PhoneOrder;
using SmartTalk.Messages.Dto.PhoneOrder;
using SmartTalk.Messages.Dto.Attachments;
using SmartTalk.Messages.Enums.PhoneOrder;
using SmartTalk.Messages.Commands.PhoneOrder;
using SmartTalk.Messages.Requests.PhoneOrder;
using SmartTalk.Messages.Commands.Attachments;
using SmartTalk.Messages.Dto.SpeechMatics;
using SmartTalk.Messages.Enums.SpeechMatics;

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
            command.RecordContent, responseFormat: TranscriptionResponseFormat.Text, cancellationToken: cancellationToken).ConfigureAwait(false);

        Log.Information("Phone order record transcription: " + transcription);
        
        if (string.IsNullOrEmpty(transcription) || transcription.Length < 15 || transcription.Contains("GENERAL") || transcription.Contains("感謝收看") || transcription.Contains("訂閱") || transcription.Contains("点赞") || transcription.Contains("立場") || transcription.Contains("字幕") || transcription.Contains("結束") || transcription.Contains("謝謝觀看") || transcription.Contains("幕後大臣") || transcription == "醒醒" || transcription == "跟著我" || transcription.Contains("政經關峻") || transcription.Contains("您拨打的电话") || transcription.Contains("Mailbox memory is full")) return;

        var transcriptionJobId = await CreateTranscriptionJobAsync(command.RecordContent, command.RecordName, cancellationToken).ConfigureAwait(false);
        
        Log.Information("Phone order record transcriptionJobId: {@transcriptionJobId}", transcriptionJobId);
            
        await _phoneOrderDataProvider.AddPhoneOrderRecordsAsync(new List<PhoneOrderRecord>
        {
            new() { SessionId = Guid.NewGuid().ToString(), Restaurant = recordInfo.Restaurant, TranscriptionText = transcription, Url = fileUrl, CreatedDate = recordInfo.OrderDate.AddHours(-7), TranscriptionJobId = transcriptionJobId}
        }, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        if (!string.IsNullOrEmpty(recordInfo.WorkWeChatRobotUrl))
            await SendWorkWeChatRobotNotifyAsync(command.RecordContent, recordInfo, transcription, cancellationToken).ConfigureAwait(false);
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