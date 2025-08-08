using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Linphone;
using SmartTalk.Messages.Requests.PhoneOrder;

namespace SmartTalk.Core.Handlers.RequestHandlers.PhoneOrder;

public class GetLinphoneDataRequestHandler : IRequestHandler<GetLinphoneDataRequest, GetLinphoneDataResponse>
{
    private readonly ILinphoneService _linphoneService;
    
    public GetLinphoneDataRequestHandler(ILinphoneService linphoneService)
    {
        _linphoneService = linphoneService;
    }
    
    public async Task<GetLinphoneDataResponse> Handle(IReceiveContext<GetLinphoneDataRequest> context, CancellationToken cancellationToken)
    {
        return await _linphoneService.GetLinphoneDataAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}