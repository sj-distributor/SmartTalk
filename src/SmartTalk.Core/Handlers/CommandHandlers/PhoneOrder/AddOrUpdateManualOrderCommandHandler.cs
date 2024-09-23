using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.PhoneOrder;
using SmartTalk.Messages.Commands.PhoneOrder;

namespace SmartTalk.Core.Handlers.CommandHandlers.PhoneOrder;

public class AddOrUpdateManualOrderCommandHandler : ICommandHandler<AddOrUpdateManualOrderCommand, AddOrUpdateManualOrderResponse>
{
    private readonly IPhoneOrderService _phoneOrderService;
    
    public AddOrUpdateManualOrderCommandHandler(IPhoneOrderService phoneOrderService)
    {
        _phoneOrderService = phoneOrderService;
    }
    
    public async Task<AddOrUpdateManualOrderResponse> Handle(IReceiveContext<AddOrUpdateManualOrderCommand> context, CancellationToken cancellationToken)
    {
        return await _phoneOrderService.AddOrUpdateManualOrderAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}