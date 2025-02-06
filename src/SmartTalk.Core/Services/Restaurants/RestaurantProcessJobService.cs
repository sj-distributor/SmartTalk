using Serilog;
using AutoMapper;
using Newtonsoft.Json;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Constants;
using SmartTalk.Core.Extensions;
using SmartTalk.Core.Services.Jobs;
using SmartTalk.Messages.Constants;
using SmartTalk.Messages.Dto.EasyPos;
using SmartTalk.Messages.Dto.Smarties;
using SmartTalk.Messages.Dto.VectorDb;
using SmartTalk.Messages.Dto.Embedding;
using SmartTalk.Core.Domain.Restaurants;
using SmartTalk.Messages.Dto.PhoneOrder;
using SmartTalk.Messages.Dto.Restaurant;
using SmartTalk.Messages.Enums.PhoneOrder;
using SmartTalk.Messages.Enums.Restaurants;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Messages.Commands.Restaurant;
using SmartTalk.Core.Services.RetrievalDb.VectorDb;

namespace SmartTalk.Core.Services.Restaurants;

public interface IRestaurantProcessJobService : IScopedDependency
{
    Task SyncRestaurantMenusAsync(SchedulingSyncRestaurantMenuCommand command, CancellationToken cancellationToken);
}

public class RestaurantProcessJobService : IRestaurantProcessJobService
{
    private readonly IMapper _mapper;
    private readonly IVectorDb _vectorDb;
    private readonly IEasyPosClient _easyPosClient;
    private readonly ISmartiesClient _smartiesClient;
    private readonly IRestaurantDataProvider _restaurantDataProvider;
    private readonly ISmartTalkBackgroundJobClient _smartTalkBackgroundJobClient;

    public RestaurantProcessJobService(IMapper mapper, IVectorDb vectorDb, IEasyPosClient easyPosClient, ISmartiesClient smartiesClient, IRestaurantDataProvider restaurantDataProvider, ISmartTalkBackgroundJobClient smartTalkBackgroundJobClient)
    {
        _mapper = mapper;
        _vectorDb = vectorDb;
        _easyPosClient = easyPosClient;
        _smartiesClient = smartiesClient;
        _restaurantDataProvider = restaurantDataProvider;
        _smartTalkBackgroundJobClient = smartTalkBackgroundJobClient;
    }

    public async Task SyncRestaurantMenusAsync(SchedulingSyncRestaurantMenuCommand command, CancellationToken cancellationToken)
    {
        foreach (var restaurant in Enum.GetValues(typeof(PhoneOrderRestaurant)).Cast<PhoneOrderRestaurant>())
            _smartTalkBackgroundJobClient.Enqueue(() => PersistRestaurantMenuItemsAsync(restaurant, cancellationToken), HangfireConstants.InternalHostingRestaurant);
    }

    public async Task PersistRestaurantMenuItemsAsync(PhoneOrderRestaurant restaurantType, CancellationToken cancellationToken)
    {
        var restaurant = await _restaurantDataProvider.GetRestaurantByNameAsync(restaurantType.GetDescription(), cancellationToken).ConfigureAwait(false);
        
        Log.Information("Get restaurant by restaurant type: {@Restaurant}, {RestaurantType}", restaurant, restaurantType);

        if (restaurant == null) throw new Exception("Could not get the restaurant by restaurant tye name");
        
        var response = await _easyPosClient.GetEasyPosRestaurantMenusAsync(restaurantType, cancellationToken).ConfigureAwait(false);

        response.Data.Products = response.Data.Products.Where(x => x.IsIndependentSale).ToList();
        
        Log.Information("Get easy pos menu item response: {@Response}", response);

        var allItems = await _restaurantDataProvider.GetRestaurantMenuItemsAsync(restaurant.Id, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        foreach (var item in allItems)
            await _vectorDb.DeleteAsync(restaurant.Id.ToString(), new VectorRecordDto { Id = item.Id.ToString() }, cancellationToken).ConfigureAwait(false);
        
        var items = new[] { "zh_CN", "en_US" }
            .SelectMany(language => response.Data.Products.SelectMany(x => GenerateMenuItems(x, language, restaurant.Id))).ToList();
        
        Log.Information("Get menu items: {@Items}", items);
        
        await _restaurantDataProvider.DeleteRestaurantMenuItemsAsync(allItems, cancellationToken: cancellationToken).ConfigureAwait(false);
        await _restaurantDataProvider.AddRestaurantMenuItemsAsync(items, cancellationToken: cancellationToken).ConfigureAwait(false);

        foreach (var item in items)
            _smartTalkBackgroundJobClient.Enqueue(() => StoreAsync(restaurant, item, cancellationToken), HangfireConstants.InternalHostingRestaurant);
    }

    public async Task StoreAsync(Restaurant restaurant, RestaurantMenuItem item, CancellationToken cancellationToken)
    {
        var record = new VectorRecordDto { Id = item.Id.ToString() };
        
        var response = await _smartiesClient.GetEmbeddingAsync(new AskGptEmbeddingRequestDto { Input = item.Name }, cancellationToken).ConfigureAwait(false);
        
        if (response == null || !response.Data.Data.Any()) throw new Exception($"Failed to embed content with id {item.Id}");
        
        record.Vector = new EmbeddingDto(response.Data.Data.First().Embedding.ToArray());

        record.Payload[VectorDbStore.ReservedRestaurantPayload] = _mapper.Map<RestaurantPayloadDto>(item);
        
        await _vectorDb.UpsertAsync(restaurant.Id.ToString(), record, cancellationToken).ConfigureAwait(false);
    }

    private string GetMenuItemName(List<EasyPosResponseLocalization> localizations, string languageCode)
    {
        var name = localizations.Where(l => l.LanguageCode == languageCode && l.Field == "name").FirstOrDefault()?.Value;
        var posName = localizations.Where(l => l.LanguageCode == languageCode && l.Field == "posName").FirstOrDefault()?.Value;
        var sendChefName = localizations.Where(l => l.LanguageCode == languageCode && l.Field == "sendChefName").FirstOrDefault()?.Value;

        return !string.IsNullOrEmpty(posName) ? posName : !string.IsNullOrEmpty(name) ? name : !string.IsNullOrEmpty(sendChefName) ? sendChefName : string.Empty;
    }
    
    private List<RestaurantMenuItem> GenerateMenuItems(EasyPosResponseProduct product, string language, int restaurantId)
    {
        var menuItems = new List<RestaurantMenuItem>();
        var languageEnum = language == "zh_CN" ? RestaurantItemLanguage.Chinese : RestaurantItemLanguage.English;

        if (product.Price == 0 && product.ModifierGroups.Any())
        {
            var modifierGroup = product.ModifierGroups.First();

            var orderItemModifiers = new PhoneCallOrderItemModifiers
            {
                Quantity = 1,
                ModifierId = modifierGroup.Id,
                Localizations = modifierGroup.Localizations
            };
            
            foreach (var modifierProduct in modifierGroup.ModifierProducts)
            {
                orderItemModifiers.Price = modifierProduct.Price;
                orderItemModifiers.ModifierProductId = modifierProduct.Id;
                orderItemModifiers.ModifierLocalizations = modifierProduct.Localizations;
                
                menuItems.Add(new RestaurantMenuItem
                {
                    RestaurantId = restaurantId,
                    Price = modifierProduct.Price,
                    ProductId = product.Id,
                    OrderItemModifiers = JsonConvert.SerializeObject(orderItemModifiers),
                    Name = $"{GetMenuItemName(product.Localizations, language)} {GetMenuItemName(modifierProduct.Localizations, language)}",
                    Language = languageEnum
                });
            }
        }
        else
        {
            menuItems.Add(new RestaurantMenuItem
            {
                RestaurantId = restaurantId,
                Price = product.Price,
                ProductId = product.Id,
                Name = GetMenuItemName(product.Localizations, language),
                Language = languageEnum
            });
        }

        return menuItems;
    }
}