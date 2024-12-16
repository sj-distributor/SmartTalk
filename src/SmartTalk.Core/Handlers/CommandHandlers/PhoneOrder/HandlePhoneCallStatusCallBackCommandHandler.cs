using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Messages.Commands.Twilio;
using SmartTalk.Core.Services.Communication;

namespace SmartTalk.Core.Handlers.CommandHandlers.PhoneOrder;

public class HandlePhoneCallStatusCallBackCommandHandler : ICommandHandler<HandlePhoneCallStatusCallBackCommand>
{
    private readonly ICommunicationProviderSwitcher _communicationProviderSwitcher;

    public HandlePhoneCallStatusCallBackCommandHandler(ICommunicationProviderSwitcher communicationProviderSwitcher)
    {
        _communicationProviderSwitcher = communicationProviderSwitcher;
    }

    public async Task Handle(IReceiveContext<HandlePhoneCallStatusCallBackCommand> context, CancellationToken cancellationToken)
    {
        await _communicationProviderSwitcher.PhoneCallProvider(context.Message.Provider)
            .HandlePhoneCallStatusCallbackAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}