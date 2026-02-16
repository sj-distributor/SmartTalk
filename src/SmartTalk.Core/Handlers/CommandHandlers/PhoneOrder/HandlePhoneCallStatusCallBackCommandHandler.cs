using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Messages.Commands.Twilio;
using SmartTalk.Core.Services.Webhook;

namespace SmartTalk.Core.Handlers.CommandHandlers.PhoneOrder;

public class HandlePhoneCallStatusCallBackCommandHandler : ICommandHandler<HandlePhoneCallStatusCallBackCommand>
{
    private readonly ITwilioWebhookService _twilioWebhookService;

    public HandlePhoneCallStatusCallBackCommandHandler(ITwilioWebhookService twilioWebhookService)
    {
        _twilioWebhookService = twilioWebhookService;
    }

    public async Task Handle(IReceiveContext<HandlePhoneCallStatusCallBackCommand> context, CancellationToken cancellationToken)
    {
        await _twilioWebhookService.HandlePhoneCallStatusCallbackAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}