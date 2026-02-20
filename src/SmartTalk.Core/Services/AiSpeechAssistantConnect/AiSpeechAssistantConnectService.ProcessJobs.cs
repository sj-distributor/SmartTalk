using Mediator.Net;
using SmartTalk.Core.Constants;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Messages.Commands.AiSpeechAssistant;
using SmartTalk.Messages.Dto.AiSpeechAssistant;

namespace SmartTalk.Core.Services.AiSpeechAssistantConnect;

public partial class AiSpeechAssistantConnectService
{
    private void TriggerTwilioRecordingPhoneCall()
    {
        _backgroundJobClient.Enqueue<IMediator>(x => 
            x.SendAsync(new RecordAiSpeechAssistantCallCommand { CallSid = _ctx.CallSid, Host = _ctx.Host }, CancellationToken.None), HangfireConstants.InternalHostingRecordPhoneCall);
    }

    private void TransferHumanService(string humanPhone, TimeSpan? delay = null)
    {
        if (delay.HasValue)
            _backgroundJobClient.Schedule<IMediator>(x =>
                x.SendAsync(new TransferHumanServiceCommand { CallSid = _ctx.CallSid, HumanPhone = humanPhone }, CancellationToken.None), delay.Value, HangfireConstants.InternalHostingTransfer);
        else
            _backgroundJobClient.Enqueue<IMediator>(x =>
                x.SendAsync(new TransferHumanServiceCommand { CallSid = _ctx.CallSid, HumanPhone = humanPhone }, CancellationToken.None), HangfireConstants.InternalHostingTransfer);
    }

    private void GenerateRecordFromCall(AiSpeechAssistantStreamContextDto streamContext)
    {
        _backgroundJobClient.Enqueue<IAiSpeechAssistantProcessJobService>(x =>
            x.RecordAiSpeechAssistantCallAsync(streamContext, _ctx.OrderRecordType, CancellationToken.None));
    }
}