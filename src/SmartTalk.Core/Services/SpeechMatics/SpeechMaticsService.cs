using Serilog;
using AutoMapper;
using SmartTalk.Core.Ioc;
using Microsoft.IdentityModel.Tokens;
using SmartTalk.Core.Domain.PhoneOrder;
using SmartTalk.Core.Extensions;
using SmartTalk.Core.Services.Ffmpeg;
using SmartTalk.Core.Services.Http;
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
    private readonly ISmartTalkHttpClientFactory _smartTalkHttpClientFactory;
    
    public SpeechMaticsService(IWeChatClient weChatClient, IFfmpegService ffmpegService, PhoneOrderSetting phoneOrderSetting, IPhoneOrderService phoneOrderService, IPhoneOrderDataProvider phoneOrderDataProvider, ISmartTalkHttpClientFactory smartTalkHttpClientFactory)
    {
        _weChatClient = weChatClient;
        _ffmpegService = ffmpegService;
        _phoneOrderSetting = phoneOrderSetting;
        _phoneOrderService = phoneOrderService;
        _phoneOrderDataProvider = phoneOrderDataProvider;
        _smartTalkHttpClientFactory = smartTalkHttpClientFactory;
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

            var audioContent = await _smartTalkHttpClientFactory.GetAsync<byte[]>(record.Url, cancellationToken).ConfigureAwait(false);
            
            await _phoneOrderService.ExtractPhoneOrderRecordAiMenuAsync(speakInfos, record, audioContent, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            record.Status = PhoneOrderRecordStatus.Exception;
            
            await _phoneOrderDataProvider.UpdatePhoneOrderRecordsAsync(record, true, cancellationToken).ConfigureAwait(false);

            Log.Warning(e.Message);
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