using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Jobs;
using SmartTalk.Core.Services.PhoneOrder;
using SmartTalk.Messages.Commands.PhoneOrder;

namespace SmartTalk.Core.Handlers.CommandHandlers.PhoneOrder;

public class ReceivePhoneOrderRecordCommandHandler : ICommandHandler<ReceivePhoneOrderRecordCommand>
{
    private readonly ISmartTalkBackgroundJobClient _backgroundJobClient;

    public ReceivePhoneOrderRecordCommandHandler(ISmartTalkBackgroundJobClient backgroundJobClient)
    {
        _backgroundJobClient = backgroundJobClient;
    }

    public async Task Handle(IReceiveContext<ReceivePhoneOrderRecordCommand> context, CancellationToken cancellationToken)
    {
        _backgroundJobClient.Enqueue<IPhoneOrderService>(x => x.ReceivePhoneOrderRecordAsync(context.Message, cancellationToken));
    }
}