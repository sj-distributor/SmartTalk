using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Constants;
using SmartTalk.Core.Services.Jobs;
using SmartTalk.Core.Services.SpeechMatics;
using SmartTalk.Messages.Commands.PhoneCall;

namespace SmartTalk.Core.Handlers.CommandHandlers.PhoneCall;

public class TranscriptionCallbackCommandHandler : ICommandHandler<HandleTranscriptionCallbackCommand>
{
    private readonly ISmartTalkBackgroundJobClient _backgroundJobClient;

    public TranscriptionCallbackCommandHandler(ISmartTalkBackgroundJobClient backgroundJobClient)
    {
        _backgroundJobClient = backgroundJobClient;
    }
    
    public async Task Handle(IReceiveContext<HandleTranscriptionCallbackCommand> context, CancellationToken cancellationToken)
    {
        _backgroundJobClient.Enqueue<ISpeechMaticsService>(x => x.HandleTranscriptionCallbackAsync(context.Message, cancellationToken), HangfireConstants.InternalHostingPhoneOrder);
    }
}
