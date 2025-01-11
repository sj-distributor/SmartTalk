using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.PhoneCall;
using SmartTalk.Messages.Commands.PhoneCall;

namespace SmartTalk.Core.Handlers.CommandHandlers.PhoneCall;

public class AddPhoneCallConversationCommandHandler : ICommandHandler<AddPhoneCallConversationsCommand, AddPhoneOrderConversationsResponse>
{
    private readonly IPhoneCallService _phoneCallService;

    public AddPhoneCallConversationCommandHandler(IPhoneCallService phoneCallService)
    {
        _phoneCallService = phoneCallService;
    }

    public async Task<AddPhoneOrderConversationsResponse> Handle(IReceiveContext<AddPhoneCallConversationsCommand> context, CancellationToken cancellationToken)
    {
        return await _phoneCallService.AddPhoneOrderConversationsAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}