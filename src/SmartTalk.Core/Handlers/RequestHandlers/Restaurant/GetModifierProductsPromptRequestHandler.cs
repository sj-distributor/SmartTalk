using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Restaurants;
using SmartTalk.Messages.Requests.Restaurant;

namespace SmartTalk.Core.Handlers.RequestHandlers.Restaurant;

public class GetModifierProductsPromptRequestHandler : IRequestHandler<GetModifierProductsPromptRequest, GetModifierProductsPromptResponse>
{
    private readonly IRestaurantService _restaurantService;

    public GetModifierProductsPromptRequestHandler(IRestaurantService restaurantService)
    {
        _restaurantService = restaurantService;
    }
    
    public async Task<GetModifierProductsPromptResponse> Handle(IReceiveContext<GetModifierProductsPromptRequest> context, CancellationToken cancellationToken)
    {
        return await _restaurantService.GetModifierProductsPromptAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}