using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Constants;
using SmartTalk.Core.Services.Jobs;
using SmartTalk.Core.Services.PhoneOrder;
using SmartTalk.Messages.Commands.PhoneOrder;

namespace SmartTalk.Core.Handlers.CommandHandlers.PhoneOrder;

public class ReceiveAixvolinkPhoneOrderRecordCommandHandler : ICommandHandler<ReceiveAixvolinkPhoneOrderRecordCommand>
{
    private readonly ISmartTalkBackgroundJobClient _backgroundJobClient;

    public ReceiveAixvolinkPhoneOrderRecordCommandHandler(ISmartTalkBackgroundJobClient backgroundJobClient)
    {
        _backgroundJobClient = backgroundJobClient;
    }

    public Task Handle(IReceiveContext<ReceiveAixvolinkPhoneOrderRecordCommand> context, CancellationToken cancellationToken)
    {
        _backgroundJobClient.Enqueue<IPhoneOrderService>(x => x.ReceiveAixvolinkPhoneOrderRecordAsync(context.Message, cancellationToken), HangfireConstants.InternalHostingAixvolinkPhoneOrder);

        return Task.CompletedTask;
    }
}
