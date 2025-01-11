using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Constants;
using SmartTalk.Core.Services.Jobs;
using SmartTalk.Core.Services.PhoneCall;
using SmartTalk.Messages.Commands.PhoneCall;

namespace SmartTalk.Core.Handlers.CommandHandlers.PhoneCall;

public class ReceivePhoneCallRecordCommandHandler : ICommandHandler<ReceivePhoneCallRecordCommand>
{
    private readonly ISmartTalkBackgroundJobClient _backgroundJobClient;

    public ReceivePhoneCallRecordCommandHandler(ISmartTalkBackgroundJobClient backgroundJobClient)
    {
        _backgroundJobClient = backgroundJobClient;
    }

    public async Task Handle(IReceiveContext<ReceivePhoneCallRecordCommand> context, CancellationToken cancellationToken)
    {
        _backgroundJobClient.Enqueue<IPhoneCallService>(x => x.ReceivePhoneOrderRecordAsync(context.Message, cancellationToken), HangfireConstants.InternalHostingPhoneOrder);
    }
}