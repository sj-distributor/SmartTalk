using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.PhoneCall;
using SmartTalk.Messages.Requests.PhoneCall;

namespace SmartTalk.Core.Handlers.RequestHandlers.PhoneOrder;

public class GetPhoneCallConversationsRequestHandler : IRequestHandler<GetPhoneCallConversationsRequest, GetPhoneCallConversationsResponse>
{
    private readonly IPhoneCallService _phoneCallService;

    public GetPhoneCallConversationsRequestHandler(IPhoneCallService phoneCallService)
    {
        _phoneCallService = phoneCallService;
    }

    public async Task<GetPhoneCallConversationsResponse> Handle(IReceiveContext<GetPhoneCallConversationsRequest> context, CancellationToken cancellationToken)
    {
        return await _phoneCallService.GetPhoneOrderConversationsAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}