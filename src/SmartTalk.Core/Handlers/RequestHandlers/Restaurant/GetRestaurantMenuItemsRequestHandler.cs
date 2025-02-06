using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Restaurants;
using SmartTalk.Messages.Requests.Restaurant;

namespace SmartTalk.Core.Handlers.RequestHandlers.Restaurant;

public class GetRestaurantMenuItemsRequestHandler : IRequestHandler<GetRestaurantMenuItemsRequest, GetRestaurantMenuItemsResponse>
{
    private readonly IRestaurantService _restaurantService;

    public GetRestaurantMenuItemsRequestHandler(IRestaurantService restaurantService)
    {
        _restaurantService = restaurantService;
    }
    
    public async Task<GetRestaurantMenuItemsResponse> Handle(IReceiveContext<GetRestaurantMenuItemsRequest> context, CancellationToken cancellationToken)
    {
        return await _restaurantService.GetRestaurantMenuItemsAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}