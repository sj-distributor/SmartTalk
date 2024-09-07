using Serilog;
using SmartTalk.Core.Domain.Restaurants;
using SmartTalk.Core.Extensions;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Messages.Commands.Restaurant;
using SmartTalk.Messages.Dto.EasyPos;
using SmartTalk.Messages.Enums.PhoneOrder;

namespace SmartTalk.Core.Services.Restaurants;

public interface IRestaurantProcessJobService : IScopedDependency
{
    Task SyncRestaurantMenusAsync(SchedulingSyncRestaurantMenuCommand command, CancellationToken cancellationToken);
}

public class RestaurantProcessJobService : IRestaurantProcessJobService
{
    private readonly IEasyPosClient _easyPosClient;
    private readonly IRestaurantDataProvider _restaurantDataProvider;

    public RestaurantProcessJobService(IEasyPosClient easyPosClient, IRestaurantDataProvider restaurantDataProvider)
    {
        _easyPosClient = easyPosClient;
        _restaurantDataProvider = restaurantDataProvider;
    }

    public async Task SyncRestaurantMenusAsync(SchedulingSyncRestaurantMenuCommand command, CancellationToken cancellationToken)
    {
        var syncTasks = Enum.GetValues(typeof(PhoneOrderRestaurant))
            .Cast<PhoneOrderRestaurant>().Select(restaurant => PersistRestaurantMenuItemsAsync(restaurant, cancellationToken));

        await Task.WhenAll(syncTasks).ConfigureAwait(false);
    }

    private async Task PersistRestaurantMenuItemsAsync(PhoneOrderRestaurant restaurantType, CancellationToken cancellationToken)
    {
        var restaurant = await _restaurantDataProvider.GetRestaurantByNameAsync(restaurantType.GetDescription(), cancellationToken).ConfigureAwait(false);
        
        Log.Information("Get restaurant by restaurant type: {@Restaurant}, {RestaurantType}", restaurant, restaurantType);

        if (restaurant == null) throw new Exception("Could not get the restaurant by restaurant tye name");
        
        var response = await _easyPosClient.GetEasyPosRestaurantMenusAsync(restaurantType, cancellationToken).ConfigureAwait(false);
        
        Log.Information("Get easy pos menu item response: {@Response}", response);
        
        var menuItems = response.Data.Products.Select(x => new RestaurantMenuItem
        {
            RestaurantId = restaurant.Id,
            Price = x.Price,
            NameEn = GetMenuItemName(x.Localizations, "en_US"),
            NameZh = GetMenuItemName(x.Localizations, "zh_CN"),
        }).ToList();

        await _restaurantDataProvider.AddRestaurantMenuItemsAsync(menuItems, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private string GetMenuItemName(List<EasyPosResponseLocalization> localizations, string languageCode)
    {
        var name = localizations.Where(l => l.LanguageCode == languageCode && l.Field == "name").FirstOrDefault()?.Value;
        var posName = localizations.Where(l => l.LanguageCode == languageCode && l.Field == "posName").FirstOrDefault()?.Value;
        var sendChefName = localizations.Where(l => l.LanguageCode == languageCode && l.Field == "sendChefName").FirstOrDefault()?.Value;

        return !string.IsNullOrEmpty(name) ? name : !string.IsNullOrEmpty(posName) ? posName : !string.IsNullOrEmpty(sendChefName) ? sendChefName : string.Empty;
    }
}