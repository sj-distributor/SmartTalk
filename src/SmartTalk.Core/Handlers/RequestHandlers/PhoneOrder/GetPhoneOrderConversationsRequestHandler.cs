using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.PhoneOrder;
using SmartTalk.Messages.Requests.PhoneOrder;

namespace SmartTalk.Core.Handlers.RequestHandlers.PhoneOrder;

public class GetPhoneOrderConversationsRequestHandler : IRequestHandler<GetPhoneOrderConversationsRequest, GetPhoneOrderConversationsResponse>
{
    private readonly IPhoneOrderService _phoneOrderService;

    public GetPhoneOrderConversationsRequestHandler(IPhoneOrderService phoneOrderService)
    {
        _phoneOrderService = phoneOrderService;
    }

    public async Task<GetPhoneOrderConversationsResponse> Handle(IReceiveContext<GetPhoneOrderConversationsRequest> context, CancellationToken cancellationToken)
    {
        return await _phoneOrderService.GetPhoneOrderConversationsAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}