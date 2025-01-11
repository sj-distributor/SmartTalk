using Serilog;
using SmartTalk.Core.Ioc;
using Microsoft.IdentityModel.Tokens;
using SmartTalk.Core.Services.Ffmpeg;
using SmartTalk.Core.Services.Http;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Core.Services.PhoneCall;
using SmartTalk.Core.Settings.PhoneCall;
using SmartTalk.Messages.Dto.SpeechMatics;
using SmartTalk.Messages.Enums.PhoneOrder;
using SmartTalk.Messages.Commands.PhoneCall;

namespace SmartTalk.Core.Services.SpeechMatics;

public interface ISpeechMaticsService : IScopedDependency
{
    Task HandleTranscriptionCallbackAsync(HandleTranscriptionCallbackCommand command, CancellationToken cancellationToken);
}

public class SpeechMaticsService : ISpeechMaticsService
{
    private readonly IWeChatClient _weChatClient;
    private readonly IFfmpegService _ffmpegService;
    private readonly PhoneCallSetting _phoneCallSetting;
    private readonly IPhoneCallService _phoneCallService;
    private readonly IPhoneCallDataProvider _phoneCallDataProvider;
    private readonly ISmartTalkHttpClientFactory _smartTalkHttpClientFactory;
    
    public SpeechMaticsService(IWeChatClient weChatClient, IFfmpegService ffmpegService, PhoneCallSetting phoneCallSetting, IPhoneCallService phoneCallService, IPhoneCallDataProvider phoneCallDataProvider, ISmartTalkHttpClientFactory smartTalkHttpClientFactory)
    {
        _weChatClient = weChatClient;
        _ffmpegService = ffmpegService;
        _phoneCallSetting = phoneCallSetting;
        _phoneCallService = phoneCallService;
        _phoneCallDataProvider = phoneCallDataProvider;
        _smartTalkHttpClientFactory = smartTalkHttpClientFactory;
    }

    public async Task HandleTranscriptionCallbackAsync(HandleTranscriptionCallbackCommand command, CancellationToken cancellationToken)
    {
        if (command.Transcription == null || command.Transcription.Results.IsNullOrEmpty() || command.Transcription.Job == null || command.Transcription.Job.Id.IsNullOrEmpty()) return;

        var record = await _phoneCallDataProvider.GetPhoneCallRecordByTranscriptionJobIdAsync(command.Transcription.Job.Id, cancellationToken).ConfigureAwait(false);

        Log.Information("Get Phone order record : {@record}", record);
        
        if (record == null) return;
        
        Log.Information("Transcription results : {@results}", command.Transcription.Results);
        
        try
        {
            record.Status = PhoneCallRecordStatus.Transcription;
            
            await _phoneCallDataProvider.UpdatePhoneCallRecordsAsync(record, true, cancellationToken).ConfigureAwait(false);
            
            var speakInfos = StructureDiarizationResults(command.Transcription.Results);

            var audioContent = await _smartTalkHttpClientFactory.GetAsync<byte[]>(record.Url, cancellationToken).ConfigureAwait(false);
            
            await _phoneCallService.ExtractPhoneOrderRecordAiMenuAsync(speakInfos, record, audioContent, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            record.Status = PhoneCallRecordStatus.Exception;
            
            await _phoneCallDataProvider.UpdatePhoneCallRecordsAsync(record, true, cancellationToken).ConfigureAwait(false);

            Log.Warning(e.Message);
        }
    }
    
    private List<SpeechMaticsSpeakInfoDto> StructureDiarizationResults(List<SpeechMaticsResultDto> results)
    {
        string currentSpeaker = null;
        PhoneCallRole? currentRole = null;
        var startTime = 0.0;
        var endTime = 0.0;
        var speakInfos = new List<SpeechMaticsSpeakInfoDto>();

        foreach (var result in results.Where(result => !result.Alternatives.IsNullOrEmpty()))
        {
            if (currentSpeaker == null)
            {
                currentSpeaker = result.Alternatives[0].Speaker;
                currentRole = PhoneCallRole.Restaurant;
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
                currentRole = currentRole == PhoneCallRole.Restaurant ? PhoneCallRole.Client : PhoneCallRole.Restaurant;
                startTime = result.StartTime;
                endTime = result.EndTime;
            }
        }

        speakInfos.Add(new SpeechMaticsSpeakInfoDto { EndTime = endTime, StartTime = startTime, Speaker = currentSpeaker });

        Log.Information("Structure diarization results : {@speakInfos}", speakInfos);
        
        return speakInfos;
    }
}