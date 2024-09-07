using SmartTalk.Core.Ioc;
using SmartTalk.Core.Domain.Restaurants;
using SmartTalk.Messages.Commands.Restaurants;
using SmartTalk.Core.Services.RetrievalDb.VectorDb;

namespace SmartTalk.Core.Services.Restaurants;

public interface IRestaurantService : IScopedDependency
{
    Task AddRestaurantAsync(AddRestaurantCommand command, CancellationToken cancellationToken);
}

public class RestaurantService : IRestaurantService
{
    private readonly IVectorDb _vectorDb;
    private readonly IRestaurantDataProvider _restaurantDataProvider;

    public RestaurantService(IVectorDb vectorDb, IRestaurantDataProvider restaurantDataProvider)
    {
        _vectorDb = vectorDb;
        _restaurantDataProvider = restaurantDataProvider;
    }

    public async Task AddRestaurantAsync(AddRestaurantCommand command, CancellationToken cancellationToken)
    {
        var restaurant = new Restaurant { Name = command.RestaurantName };
        
        await _restaurantDataProvider.AddRestaurantAsync(restaurant, cancellationToken: cancellationToken).ConfigureAwait(false);

        await _vectorDb.CreateIndexAsync(restaurant.Id.ToString(), 3072, cancellationToken).ConfigureAwait(false);
    }
}