using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Core.Services.Jobs;
using SmartTalk.Messages.Commands.AiSpeechAssistant;

namespace SmartTalk.Core.Handlers.CommandHandlers.AiSpeechAssistant;

public class ReceivePhoneRecordingStatusCallbackCommandHandler : ICommandHandler<ReceivePhoneRecordingStatusCallbackCommand>
{
    private readonly ISmartTalkBackgroundJobClient _smartTalkBackgroundJobClient;

    public ReceivePhoneRecordingStatusCallbackCommandHandler(ISmartTalkBackgroundJobClient smartTalkBackgroundJobClient)
    {
        _smartTalkBackgroundJobClient = smartTalkBackgroundJobClient;
    }

    public async Task Handle(IReceiveContext<ReceivePhoneRecordingStatusCallbackCommand> context, CancellationToken cancellationToken)
    {
        _smartTalkBackgroundJobClient.Enqueue<IAiSpeechAssistantService>(x => x.ReceivePhoneRecordingStatusCallbackAsync(context.Message, cancellationToken).ConfigureAwait(false));
    }
}