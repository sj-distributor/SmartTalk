using AutoMapper;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using SmartTalk.Core.Domain.PhoneOrder;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.PhoneOrder;
using SmartTalk.Messages.Commands.PhoneOrder;
using SmartTalk.Messages.Dto.PhoneOrder;
using SmartTalk.Messages.Dto.Speechmatics;
using SmartTalk.Messages.Enums.PhoneOrder;

namespace SmartTalk.Core.Services.Speechmatics;

public interface ISpeechMaticsService : IScopedDependency
{
    Task<TranscriptionCallbackHandledResponse> HandleTranscriptionCallbackAsync(HandleTranscriptionCallbackCommand command, CancellationToken cancellationToken);
}

public class SpeechMaticsService : ISpeechMaticsService
{
    private readonly IMapper _mapper;
    private readonly IPhoneOrderDataProvider _phoneOrderDataProvider;
    
    public SpeechMaticsService(IMapper mapper, IPhoneOrderDataProvider phoneOrderDataProvider)
    {
        _mapper = mapper;
        _phoneOrderDataProvider = phoneOrderDataProvider;
    }

    public async Task<TranscriptionCallbackHandledResponse> HandleTranscriptionCallbackAsync(HandleTranscriptionCallbackCommand command, CancellationToken cancellationToken)
    {
        
        if (command.Transcription == null || command.Transcription.Results.IsNullOrEmpty() || command.Transcription.Job == null || command.Transcription.Job.Id.IsNullOrEmpty())
            return new TranscriptionCallbackHandledResponse();

        var record = await _phoneOrderDataProvider.GetPhoneOrderRecordByTranscriptionJobIdAsync(command.Transcription.Job.Id, cancellationToken).ConfigureAwait(false);
        
        var results = command.Transcription.Results;
        
        try
        {
            if (record is null)
                throw new Exception("Record not exist");

            var speakInfos = await StructureDiarizationResultsAsync(results, record, cancellationToken).ConfigureAwait(false);
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
        
        return new TranscriptionCallbackHandledResponse();
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