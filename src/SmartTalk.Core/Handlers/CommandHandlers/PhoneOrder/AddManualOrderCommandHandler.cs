using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.PhoneOrder;
using SmartTalk.Messages.Commands.PhoneOrder;

namespace SmartTalk.Core.Handlers.CommandHandlers.PhoneOrder;

public class AddManualOrderCommandHandler : ICommandHandler<AddManualOrderCommand, AddManualOrderResponse>
{
    private readonly IPhoneOrderService _phoneOrderService;
    
    public AddManualOrderCommandHandler(IPhoneOrderService phoneOrderService)
    {
        _phoneOrderService = phoneOrderService;
    }
    
    public async Task<AddManualOrderResponse> Handle(IReceiveContext<AddManualOrderCommand> context, CancellationToken cancellationToken)
    {
        return await _phoneOrderService.AddManualOrderAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}