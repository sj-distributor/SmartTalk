using AutoMapper;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Domain.Restaurants;
using SmartTalk.Messages.Commands.Restaurants;
using SmartTalk.Core.Services.RetrievalDb.VectorDb;
using SmartTalk.Messages.Dto.Restaurant;
using SmartTalk.Messages.Requests.Restaurant;

namespace SmartTalk.Core.Services.Restaurants;

public interface IRestaurantService : IScopedDependency
{
    Task AddRestaurantAsync(AddRestaurantCommand command, CancellationToken cancellationToken);

    Task<GetRestaurantMenuItemsResponse> GetRestaurantMenuItemsAsync(GetRestaurantMenuItemsRequest request, CancellationToken cancellationToken);
}

public class RestaurantService : IRestaurantService
{
    private readonly IMapper _mapper;
    private readonly IVectorDb _vectorDb;
    private readonly IRestaurantDataProvider _restaurantDataProvider;

    public RestaurantService(IMapper mapper, IVectorDb vectorDb, IRestaurantDataProvider restaurantDataProvider)
    {
        _mapper = mapper;
        _vectorDb = vectorDb;
        _restaurantDataProvider = restaurantDataProvider;
    }

    public async Task AddRestaurantAsync(AddRestaurantCommand command, CancellationToken cancellationToken)
    {
        var restaurant = new Restaurant { Name = command.RestaurantName };
        
        await _restaurantDataProvider.AddRestaurantAsync(restaurant, cancellationToken: cancellationToken).ConfigureAwait(false);

        await _vectorDb.CreateIndexAsync(restaurant.Id.ToString(), 3072, cancellationToken).ConfigureAwait(false);
    }

    public async Task<GetRestaurantMenuItemsResponse> GetRestaurantMenuItemsAsync(GetRestaurantMenuItemsRequest request, CancellationToken cancellationToken)
    {
        var menuItems = await _restaurantDataProvider.GetRestaurantMenuItemsAsync(
            request.RestaurantId, request.PageIndex, request.PageSize, request.Keyword, cancellationToken: cancellationToken).ConfigureAwait(false);

        return new GetRestaurantMenuItemsResponse
        {
            Data = _mapper.Map<List<RestaurantMenuItemDto>>(menuItems)
        };
    }
}