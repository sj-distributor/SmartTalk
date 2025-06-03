using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Linphone;
using SmartTalk.Messages.Requests.PhoneOrder;

namespace SmartTalk.Core.Handlers.RequestHandlers.PhoneOrder;

public class GetLinphoneRestaurantNumberRequestHandler : IRequestHandler<GetLinphoneRestaurantNumberRequest, GetLinphoneRestaurantNumberResponse>
{
    private readonly ILinphoneService _linphoneService;
    
    public GetLinphoneRestaurantNumberRequestHandler(ILinphoneService linphoneService)
    {
        _linphoneService = linphoneService;
    }
    
    public async Task<GetLinphoneRestaurantNumberResponse> Handle(IReceiveContext<GetLinphoneRestaurantNumberRequest> context, CancellationToken cancellationToken)
    {
         return await _linphoneService.GetLinphoneRestaurantNumberAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}