using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Twilio;
using SmartTalk.Messages.Requests.Twilio;

namespace SmartTalk.Core.Handlers.RequestHandlers.Twilio;

public class SendTwilioMessageRequestHandler : IRequestHandler<SendTwilioMessageRequest, SendTwilioMessageResponse>
{
    private readonly ITwilioService _twilioService;

    public SendTwilioMessageRequestHandler(ITwilioService twilioService)
    {
        _twilioService = twilioService;
    }

    public async Task<SendTwilioMessageResponse> Handle(IReceiveContext<SendTwilioMessageRequest> context, CancellationToken cancellationToken)
    {
        return await _twilioService.SendMessageAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}