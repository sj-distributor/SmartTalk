using Serilog;
using AutoMapper;
using SmartTalk.Core.Ioc;
using Microsoft.IdentityModel.Tokens;
using SmartTalk.Core.Domain.PhoneOrder;
using SmartTalk.Core.Extensions;
using SmartTalk.Core.Services.Ffmpeg;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Messages.Dto.PhoneOrder;
using SmartTalk.Core.Services.PhoneOrder;
using SmartTalk.Core.Settings.PhoneOrder;
using SmartTalk.Messages.Dto.SpeechMatics;
using SmartTalk.Messages.Enums.PhoneOrder;
using SmartTalk.Messages.Commands.PhoneOrder;
using SmartTalk.Messages.Dto.WeChat;
using SmartTalk.Messages.Enums.WeChat;

namespace SmartTalk.Core.Services.SpeechMatics;

public interface ISpeechMaticsService : IScopedDependency
{
    Task HandleTranscriptionCallbackAsync(HandleTranscriptionCallbackCommand command, CancellationToken cancellationToken);
}

public class SpeechMaticsService : ISpeechMaticsService
{
    private readonly  IWeChatClient _weChatClient;
    private  readonly IFfmpegService _ffmpegService;
    private readonly PhoneOrderSetting _phoneOrderSetting;
    private readonly IPhoneOrderService _phoneOrderService;
    private readonly IPhoneOrderDataProvider _phoneOrderDataProvider;
    
    public SpeechMaticsService(IWeChatClient weChatClient, IFfmpegService ffmpegService, PhoneOrderSetting phoneOrderSetting, IPhoneOrderService phoneOrderService, IPhoneOrderDataProvider phoneOrderDataProvider)
    {
        _weChatClient = weChatClient;
        _ffmpegService = ffmpegService;
        _phoneOrderSetting = phoneOrderSetting;
        _phoneOrderService = phoneOrderService;
        _phoneOrderDataProvider = phoneOrderDataProvider;
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

            var recordContent = await _phoneOrderService.ExtractPhoneOrderRecordAiMenuAsync(speakInfos, record, cancellationToken).ConfigureAwait(false);
            
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
            }, record.TranscriptionText, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            record.Status = PhoneOrderRecordStatus.Exception;
            await _phoneOrderDataProvider.UpdatePhoneOrderRecordsAsync(record, true, cancellationToken).ConfigureAwait(false);

            Log.Warning(e.Message);
        }
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
}