using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Restaurants;
using SmartTalk.Messages.Commands.Restaurants;

namespace SmartTalk.Core.Handlers.CommandHandlers.Restaurant;

public class SchedulingSyncRestaurantMenuCommandHandler : ICommandHandler<SchedulingSyncRestaurantMenuCommand>
{
    private readonly IRestaurantProcessJobService _restaurantProcessJobService;

    public SchedulingSyncRestaurantMenuCommandHandler(IRestaurantProcessJobService restaurantProcessJobService)
    {
        _restaurantProcessJobService = restaurantProcessJobService;
    }

    public async Task Handle(IReceiveContext<SchedulingSyncRestaurantMenuCommand> context, CancellationToken cancellationToken)
    {
        await _restaurantProcessJobService.SyncRestaurantMenusAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}