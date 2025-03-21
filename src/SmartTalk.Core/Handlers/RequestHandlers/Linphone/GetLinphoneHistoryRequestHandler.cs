using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Linphone;
using SmartTalk.Messages.Requests.Linphone;

namespace SmartTalk.Core.Handlers.RequestHandlers.Linphone;

public class GetLinphoneHistoryRequestHandler : IRequestHandler<GetLinphoneHistoryRequest, GetLinphoneHistoryResponse>
{
    private readonly ILinphoneService _linphoneService;
    
    public GetLinphoneHistoryRequestHandler(ILinphoneService linphoneService)
    {
        _linphoneService = linphoneService;
    }
    
    public async Task<GetLinphoneHistoryResponse> Handle(IReceiveContext<GetLinphoneHistoryRequest> context, CancellationToken cancellationToken)
    {
        return await _linphoneService.GetLinphoneHistoryAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}