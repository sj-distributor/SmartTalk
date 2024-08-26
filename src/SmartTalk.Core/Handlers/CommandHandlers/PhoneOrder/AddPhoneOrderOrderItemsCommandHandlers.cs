using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.PhoneOrder;
using SmartTalk.Messages.Commands.PhoneOrder;

namespace SmartTalk.Core.Handlers.CommandHandlers.PhoneOrder;

public class AddPhoneOrderOrderItemsCommandHandlers : ICommandHandler<AddPhoneOrderOrderItemsCommand, AddPhoneOrderOrderItemsResponse>
{
    private readonly IPhoneOrderService _phoneOrderService;

    public AddPhoneOrderOrderItemsCommandHandlers(IPhoneOrderService phoneOrderService)
    {
        _phoneOrderService = phoneOrderService;
    }

    public async Task<AddPhoneOrderOrderItemsResponse> Handle(IReceiveContext<AddPhoneOrderOrderItemsCommand> context, CancellationToken cancellationToken)
    {
        return await _phoneOrderService.AddPhoneOrderOrderItemsAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}