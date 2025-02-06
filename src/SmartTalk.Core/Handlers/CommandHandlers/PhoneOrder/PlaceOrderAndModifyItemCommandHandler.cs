using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.PhoneOrder;
using SmartTalk.Messages.Commands.PhoneOrder;

namespace SmartTalk.Core.Handlers.CommandHandlers.PhoneOrder;

public class PlaceOrderAndModifyItemCommandHandler : ICommandHandler<PlaceOrderAndModifyItemCommand, PlaceOrderAndModifyItemResponse>
{
    private readonly IPhoneOrderService _phoneOrderService;

    public PlaceOrderAndModifyItemCommandHandler(IPhoneOrderService phoneOrderService)
    {
        _phoneOrderService = phoneOrderService;
    }
    
    public async Task<PlaceOrderAndModifyItemResponse> Handle(IReceiveContext<PlaceOrderAndModifyItemCommand> context, CancellationToken cancellationToken)
    {
        return await _phoneOrderService.PlaceOrderAndModifyItemsAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}