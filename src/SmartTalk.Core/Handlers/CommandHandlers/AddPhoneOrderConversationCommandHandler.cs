using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.PhoneOrder;
using SmartTalk.Messages.Commands;

namespace SmartTalk.Core.Handlers.CommandHandlers;

public class AddPhoneOrderConversationCommandHandler : ICommandHandler<AddPhoneOrderConversationsCommand, AddPhoneOrderConversationsResponse>
{
    private readonly IPhoneOrderService _phoneOrderService;

    public AddPhoneOrderConversationCommandHandler(IPhoneOrderService phoneOrderService)
    {
        _phoneOrderService = phoneOrderService;
    }

    public async Task<AddPhoneOrderConversationsResponse> Handle(IReceiveContext<AddPhoneOrderConversationsCommand> context, CancellationToken cancellationToken)
    {
        return await _phoneOrderService.AddPhoneOrderConversationsAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}