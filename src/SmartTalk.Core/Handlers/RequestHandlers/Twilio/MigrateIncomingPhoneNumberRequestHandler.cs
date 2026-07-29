using Mediator.Net.Context;
using Mediator.Net.Contracts;
using Serilog;
using SmartTalk.Core.Services.Twilio;
using SmartTalk.Messages.Requests.Twilio;

namespace SmartTalk.Core.Handlers.RequestHandlers.Twilio;

public class MigrateIncomingPhoneNumberRequestHandler : IRequestHandler<MigrateIncomingPhoneNumberRequest, MigrateIncomingPhoneNumberResponse>
{
    private readonly ITwilioService _twilioService;

    public MigrateIncomingPhoneNumberRequestHandler(ITwilioService twilioService)
    {
        _twilioService = twilioService;
    }

    public async Task<MigrateIncomingPhoneNumberResponse> Handle(IReceiveContext<MigrateIncomingPhoneNumberRequest> context, CancellationToken cancellationToken)
    {
        return await _twilioService.MigrateIncomingPhoneNumberAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}
