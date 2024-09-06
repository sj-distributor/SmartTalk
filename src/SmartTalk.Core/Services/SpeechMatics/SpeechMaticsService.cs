using Serilog;
using AutoMapper;
using SmartTalk.Core.Ioc;
using Microsoft.IdentityModel.Tokens;
using SmartTalk.Core.Domain.PhoneOrder;
using SmartTalk.Messages.Dto.PhoneOrder;
using SmartTalk.Core.Services.PhoneOrder;
using SmartTalk.Messages.Dto.SpeechMatics;
using SmartTalk.Messages.Enums.PhoneOrder;
using SmartTalk.Messages.Commands.PhoneOrder;

namespace SmartTalk.Core.Services.SpeechMatics;

public interface ISpeechMaticsService : IScopedDependency
{
    Task HandleTranscriptionCallbackAsync(HandleTranscriptionCallbackCommand command, CancellationToken cancellationToken);
}

public class SpeechMaticsService : ISpeechMaticsService
{
    private readonly IPhoneOrderService _phoneOrderService;
    private readonly IPhoneOrderDataProvider _phoneOrderDataProvider;
    
    public SpeechMaticsService(IPhoneOrderService phoneOrderService, IPhoneOrderDataProvider phoneOrderDataProvider)
    {
        _phoneOrderService = phoneOrderService;
        _phoneOrderDataProvider = phoneOrderDataProvider;
    }

    public async Task HandleTranscriptionCallbackAsync(HandleTranscriptionCallbackCommand command, CancellationToken cancellationToken)
    {
        if (command.Transcription == null || command.Transcription.Results.IsNullOrEmpty() || command.Transcription.Job == null || command.Transcription.Job.Id.IsNullOrEmpty()) return;

        var record = await _phoneOrderDataProvider.GetPhoneOrderRecordByTranscriptionJobIdAsync(command.Transcription.Job.Id, cancellationToken).ConfigureAwait(false);
        
        Log.Information("Get Phone order record : {@record}", record);
        
        var results = command.Transcription.Results;
        
        Log.Information("Transcription results : {@results}", results);
        
        try
        {
            if (record is null) throw new Exception("Record not exist");

            var speakInfos = await StructureDiarizationResultsAsync(results, record, cancellationToken).ConfigureAwait(false);
            
            Log.Information("speakInfos : {@speakInfos}", speakInfos);

            await _phoneOrderService.ExtractPhoneOrderRecordAiMenuAsync(speakInfos, record, cancellationToken).ConfigureAwait(false);
            
            // send放到这里
            record.Status = PhoneOrderRecordStatus.Sent;
            await _phoneOrderDataProvider.UpdatePhoneOrderRecordsAsync(record, true, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            if (record is not null)
            {
                record.Status = PhoneOrderRecordStatus.Exception;
                await _phoneOrderDataProvider.UpdatePhoneOrderRecordsAsync(record, true, cancellationToken).ConfigureAwait(false);
            }
            
            Log.Warning(e.Message);
        }
    }

    private async Task<List<SpeechMaticsSpeakInfoDto>> StructureDiarizationResultsAsync(List<SpeechMaticsResultDto> results, PhoneOrderRecord record, CancellationToken cancellationToken)
    {
        string currentSpeaker = null;
        var startTime = 0.0;
        var endTime = 0.0;
        var speakInfos = new List<SpeechMaticsSpeakInfoDto>();

        foreach (var result in results.Where(result => !result.Alternatives.IsNullOrEmpty()))
        {
            if (currentSpeaker == null)
            {
                currentSpeaker = result.Alternatives[0].Speaker;
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
                speakInfos.Add(new SpeechMaticsSpeakInfoDto { EndTime = endTime, StartTime = startTime, Speaker = currentSpeaker });
                currentSpeaker = result.Alternatives[0].Speaker;
                startTime = result.StartTime;
                endTime = result.EndTime;
            }
        }

        speakInfos.Add(new SpeechMaticsSpeakInfoDto { EndTime = endTime, StartTime = startTime, Speaker = currentSpeaker });

        record.Status = PhoneOrderRecordStatus.Transcription;
        await _phoneOrderDataProvider.UpdatePhoneOrderRecordsAsync(record, true, cancellationToken).ConfigureAwait(false);

        return speakInfos;
    }
}