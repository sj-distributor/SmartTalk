using SmartTalk.Messages.Commands.Twilio;
using SmartTalk.Messages.DTO.Communication.Twilio;
using SmartTalk.Core.Services.Communication.Twilio;
using SmartTalk.Messages.Enums.Communication.PhoneCall;

namespace SmartTalk.Core.Services.Communication.Providers.Twilio;

public class TwilioPhoneCallProvider : IPhoneCallProvider
{
    private readonly ITwilioService _twilioService;

    public TwilioPhoneCallProvider(ITwilioService twilioService)
    {
        _twilioService = twilioService;
    }

    public async Task HandlePhoneCallStatusCallbackAsync(HandlePhoneCallStatusCallBackCommand command, CancellationToken cancellationToken)
    {
        await _twilioService.HandlePhoneCallStatusCallbackAsync((TwilioPhoneCallStatusCallbackDto)command.CallBackMessage, cancellationToken).ConfigureAwait(false);
    }
    
    public PhoneCallProvider PhoneCallProvider => PhoneCallProvider.Twilio;
}