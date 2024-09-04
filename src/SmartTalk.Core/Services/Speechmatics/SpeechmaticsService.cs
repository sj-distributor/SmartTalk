using AutoMapper;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.PhoneOrder;
using SmartTalk.Messages.Commands.PhoneOrder;
using SmartTalk.Messages.Dto.Speechmatics;
using SmartTalk.Messages.Enums.PhoneOrder;

namespace SmartTalk.Core.Services.Speechmatics;

public interface ISpeechmaticsService : IScopedDependency
{
    Task<TranscriptionCallbackResponse> TranscriptionCallbackAsync(TranscriptionCallbackCommand command, CancellationToken cancellationToken);
}

public class SpeechmaticsService : ISpeechmaticsService
{
    private readonly IMapper _mapper;
    private readonly IPhoneOrderDataProvider _phoneOrderDataProvider;
    
    public SpeechmaticsService(IMapper mapper, IPhoneOrderDataProvider phoneOrderDataProvider)
    {
        _mapper = mapper;
        _phoneOrderDataProvider = phoneOrderDataProvider;
    }

    public async Task<TranscriptionCallbackResponse> TranscriptionCallbackAsync(TranscriptionCallbackCommand command, CancellationToken cancellationToken)
    {
        try
        {
            if (command.Transcription == null || command.Transcription.Results.IsNullOrEmpty() || command.Transcription.Job == null || command.Transcription.Job.Id.IsNullOrEmpty())
                return new TranscriptionCallbackResponse();

            var record = await _phoneOrderDataProvider.GetPhoneOrderRecordByTranscriptionJobIdAsync(command.Transcription.Job.Id, cancellationToken).ConfigureAwait(false);

            if (record is null)
                throw new Exception("Record not exist");

            var results = command.Transcription.Results;

            string currentSpeaker = null;
            var startTime = 0.0;
            var endTime = 0.0;
            var speakInfos = new List<SpeechmaticsSpeakInfoDto>();

            foreach (var result in results)
            {
                if (result.Alternatives.IsNullOrEmpty())
                    continue;

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
                    speakInfos.Add(new SpeechmaticsSpeakInfoDto { EndTime = endTime, StartTime = startTime, Speaker = currentSpeaker });
                    currentSpeaker = result.Alternatives[0].Speaker;
                    startTime = result.StartTime;
                    endTime = result.EndTime;
                }
            }

            speakInfos.Add(new SpeechmaticsSpeakInfoDto { EndTime = endTime, StartTime = startTime, Speaker = currentSpeaker });

            record.Status = PhoneOrderRecordStatus.Transcription;
            await _phoneOrderDataProvider.UpdatePhoneOrderRecordsAsync(record, true, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            if (command.Transcription != null && command.Transcription.Job != null && !command.Transcription.Job.Id.IsNullOrEmpty())
            {
                var record = await _phoneOrderDataProvider.GetPhoneOrderRecordByTranscriptionJobIdAsync(command.Transcription.Job.Id, cancellationToken).ConfigureAwait(false);
                
                if (record is not null)
                {
                    record.Status = PhoneOrderRecordStatus.Exception;
                    await _phoneOrderDataProvider.UpdatePhoneOrderRecordsAsync(record, true, cancellationToken).ConfigureAwait(false);
                }
            }
            
            Log.Warning(e.Message);
        }

        return new TranscriptionCallbackResponse();
    }
}