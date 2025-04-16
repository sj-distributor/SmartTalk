using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Restaurants;
using SmartTalk.Messages.Requests.Restaurant;

namespace SmartTalk.Core.Handlers.RequestHandlers.Restaurant;

public class GetModifierProductsPromptRequestHandler : IRequestHandler<GetRestaurantMenuItemSpecificationRequest, GetRestaurantMenuItemSpecificationResponse>
{
    private readonly IRestaurantService _restaurantService;

    public GetModifierProductsPromptRequestHandler(IRestaurantService restaurantService)
    {
        _restaurantService = restaurantService;
    }
    
    public async Task<GetRestaurantMenuItemSpecificationResponse> Handle(IReceiveContext<GetRestaurantMenuItemSpecificationRequest> context, CancellationToken cancellationToken)
    {
        return await _restaurantService.GetModifierProductsPromptAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}