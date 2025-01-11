using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.PhoneCall;
using SmartTalk.Messages.Commands.PhoneCall;

namespace SmartTalk.Core.Handlers.CommandHandlers.PhoneCall;

public class AddOrUpdateManualOrderCommandHandler : ICommandHandler<AddOrUpdateManualOrderCommand, AddOrUpdateManualOrderResponse>
{
    private readonly IPhoneCallService _phoneCallService;
    
    public AddOrUpdateManualOrderCommandHandler(IPhoneCallService phoneCallService)
    {
        _phoneCallService = phoneCallService;
    }
    
    public async Task<AddOrUpdateManualOrderResponse> Handle(IReceiveContext<AddOrUpdateManualOrderCommand> context, CancellationToken cancellationToken)
    {
        return await _phoneCallService.AddOrUpdateManualOrderAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}