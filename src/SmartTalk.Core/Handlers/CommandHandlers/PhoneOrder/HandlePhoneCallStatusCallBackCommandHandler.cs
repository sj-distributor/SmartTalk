using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Messages.Commands.Twilio;
using SmartTalk.Core.Services.Communication.Twilio;

namespace SmartTalk.Core.Handlers.CommandHandlers.PhoneOrder;

public class HandlePhoneCallStatusCallBackCommandHandler : ICommandHandler<HandlePhoneCallStatusCallBackCommand>
{
    private readonly ITwilioService _twilioService;

    public HandlePhoneCallStatusCallBackCommandHandler(ITwilioService twilioService)
    {
        _twilioService = twilioService;
    }

    public async Task Handle(IReceiveContext<HandlePhoneCallStatusCallBackCommand> context, CancellationToken cancellationToken)
    {
        await _twilioService.HandlePhoneCallStatusCallbackAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}