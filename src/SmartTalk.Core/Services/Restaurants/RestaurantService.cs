using System.Text;
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
    
    Task<GetModifierProductsPromptResponse> GetModifierProductsPromptAsync(GetModifierProductsPromptRequest request, CancellationToken cancellationToken);
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
        var (count, menuItems) = await _restaurantDataProvider.GetRestaurantMenuItemsInPageAsync(
            request.PageIndex, request.PageSize, request.RestaurantId, request.Keyword, cancellationToken: cancellationToken).ConfigureAwait(false);

        return new GetRestaurantMenuItemsResponse
        {
            Data = new GetRestaurantMenuItemsResponseData
            {
                Count = count,
                MenuItems = _mapper.Map<List<RestaurantMenuItemDto>>(menuItems)
            }
        };
    }

    public async Task<GetModifierProductsPromptResponse> GetModifierProductsPromptAsync(GetModifierProductsPromptRequest request, CancellationToken cancellationToken) 
    { 
        var groups = await _restaurantDataProvider.GetModifierProductsGroupsAsync(request.RestaurantName, cancellationToken).ConfigureAwait(false);
        
        var promptDict = new Dictionary<string, StringBuilder>();
        
        foreach (var group in groups) 
        { 
            var validTimePeriods = group.TimePeriods?
                .Where(tp => tp.DayOfWeeks.Contains((int)DateTime.UtcNow.DayOfWeek))
                .Where(tp => IsWithinTimeRange(tp.StartTime, tp.EndTime))
                .ToList();

            if (validTimePeriods == null || !validTimePeriods.Any()) continue; 
            
            if (!promptDict.ContainsKey(group.LanguageCode)) promptDict[group.LanguageCode] = new StringBuilder();
            
            var stringBuilder = promptDict[group.LanguageCode];
            
            if (group.ModifierItems.Count > 0 && group.MinimumSelect > 0) 
            { 
                var sides = string.Join(", ", group.ModifierItems.Select(i => i.Name)); 
                stringBuilder.AppendLine($"{group.GroupName}, priced at {group.ItemPrice} dollar, served with your choice of {group.MinimumSelect} sides: {sides}. Choose exactly {group.MinimumSelect}."); 
                stringBuilder.AppendLine(); 
            }
            
            foreach (var item in group.ModifierItems.Where(i => i.Price > 0)) 
            { 
                if (item.OriginalPrice.HasValue && item.OriginalPrice.Value > item.Price) 
                { 
                    stringBuilder.AppendLine($"{group.GroupName}, priced at {group.ItemPrice} dollar, available with sides: {item.Name} (original price {item.OriginalPrice.Value} dollar, current add-on {item.Price} dollar)."); 
                }
                else 
                { 
                    stringBuilder.AppendLine($"{group.GroupName}, priced at {group.ItemPrice} dollar, available with sides: {item.Name} (add-on {item.Price} dollar)."); 
                } 
                stringBuilder.AppendLine(); 
            }
            
            var sizeVariants = group.ModifierItems
                .Where(i => !string.IsNullOrEmpty(i.Size))
                .GroupBy(i => i.Size)
                .Select(g => $"{g.Key} {g.First().Price} dollar")
                .ToList();
            
            if (sizeVariants.Any()) 
            { 
                stringBuilder.AppendLine($"{group.GroupName} (Size): {string.Join(", ", sizeVariants)}."); 
                stringBuilder.AppendLine(); 
            } 
            
            if (validTimePeriods.Any())
            {
                var timePeriodPrompt = string.Join(", ", validTimePeriods.Select(tp => $"{tp.Name} ({tp.StartTime} - {tp.EndTime})"));
                stringBuilder.AppendLine($"Available during: {timePeriodPrompt}");
                stringBuilder.AppendLine();
            }
        }
        
        var result = promptDict
            .Select(kv => new LocalizedPrompt 
            { 
                LanguageCode = kv.Key, 
                Prompt = kv.Value.ToString().Trim() 
            }).ToList();
        
        return new GetModifierProductsPromptResponse 
        { 
            Prompts = result 
        }; 
    }
    
    private bool IsWithinTimeRange(string startTime, string endTime)
    {
        if (!TimeSpan.TryParse(startTime, out var start) || !TimeSpan.TryParse(endTime, out var end))
            return false;

        var now = DateTime.UtcNow.TimeOfDay;

        return start <= end
            ? now >= start && now <= end
            : now >= start || now <= end;
    }
}