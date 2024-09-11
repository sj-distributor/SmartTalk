using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Restaurants;
using SmartTalk.Messages.Commands.Restaurants;

namespace SmartTalk.Core.Handlers.CommandHandlers.Restaurant;

public class AddRestaurantCommandHandler : ICommandHandler<AddRestaurantCommand>
{
    private readonly IRestaurantService _restaurantService;

    public AddRestaurantCommandHandler(IRestaurantService restaurantService)
    {
        _restaurantService = restaurantService;
    }

    public async Task Handle(IReceiveContext<AddRestaurantCommand> context, CancellationToken cancellationToken)
    {
        await _restaurantService.AddRestaurantAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}