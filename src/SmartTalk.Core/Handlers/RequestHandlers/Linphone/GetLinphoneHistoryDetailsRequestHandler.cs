using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Linphone;
using SmartTalk.Messages.Requests.Linphone;

namespace SmartTalk.Core.Handlers.RequestHandlers.Linphone;

public class GetLinphoneHistoryDetailsRequestHandler : IRequestHandler<GetLinphoneHistoryDetailsRequest, GetLinphoneHistoryDetailsResponse>
{
    private readonly ILinphoneService _LinphoneService;
    
    public GetLinphoneHistoryDetailsRequestHandler(ILinphoneService linphoneService)
    {
        _LinphoneService = linphoneService;
    }
    
    public async Task<GetLinphoneHistoryDetailsResponse> Handle(IReceiveContext<GetLinphoneHistoryDetailsRequest> context, CancellationToken cancellationToken)
    {
        return await _LinphoneService.GetLinphoneHistoryDetailsAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}