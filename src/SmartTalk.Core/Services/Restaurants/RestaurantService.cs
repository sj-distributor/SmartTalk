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
    
    Task<GetRestaurantMenuItemSpecificationResponse> GetRestaurantMenuItemSpecificationAsync(GetRestaurantMenuItemSpecificationRequest request, CancellationToken cancellationToken);
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

    public async Task<GetRestaurantMenuItemSpecificationResponse> GetRestaurantMenuItemSpecificationAsync(GetRestaurantMenuItemSpecificationRequest request, CancellationToken cancellationToken) 
    { 
        var groups = await _restaurantDataProvider.GetRestaurantMenuItemSpecificationAsync(request.RestaurantName, cancellationToken).ConfigureAwait(false);
        
        if (!string.IsNullOrEmpty(request.LanguageCode))
                groups = groups.Where(g => g.LanguageCode == request.LanguageCode).ToList();
        
        var promptDict = new Dictionary<string, StringBuilder>();
        
        foreach (var group in groups)
        {
            if (!promptDict.ContainsKey(group.LanguageCode))
                promptDict[group.LanguageCode] = new StringBuilder();

            var stringBuilder = promptDict[group.LanguageCode];
            
            AppendGroupDescription(group, stringBuilder);
            AppendItemDescription(group, stringBuilder);
            AppendSizeVariants(group, stringBuilder);
            AppendTimePeriod(group, stringBuilder);
        }
        
        var result = promptDict
            .Select(kv => new LocalizedPrompt 
            { 
                LanguageCode = kv.Key, 
                Prompt = kv.Value.ToString().Trim() 
            }).ToList();
        
        return new GetRestaurantMenuItemSpecificationResponse 
        { 
            Prompts = result 
        }; 
    }
    
    private void AppendGroupDescription(RestaurantMenuItemSpecificationDto group, StringBuilder stringBuilder)
    {
        if (group.ModifierItems.Count > 0 && group.MinimumSelect > 0)
        {
            var sides = string.Join(", ", group.ModifierItems.Select(i => i.Name));
            stringBuilder.AppendLine($"{group.GroupName}, priced at {group.ItemPrice} dollar, served with your choice of {group.MinimumSelect} sides: {sides}. Choose exactly {group.MinimumSelect}.");
            
            if (group.MaximumRepetition == 1)
            {
                stringBuilder.AppendLine("Each item can only be selected once.");
            }
            else if (group.MaximumRepetition == 0)
            {
                stringBuilder.AppendLine("Items can be selected multiple times.");
            }

            AppendTimePeriod(group, stringBuilder);
            stringBuilder.AppendLine();
        }
    }

    private void AppendItemDescription(RestaurantMenuItemSpecificationDto group, StringBuilder stringBuilder)
    {
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
            AppendTimePeriod(group, stringBuilder);
            stringBuilder.AppendLine();
        }
    }

    private void AppendSizeVariants(RestaurantMenuItemSpecificationDto group, StringBuilder stringBuilder)
    {
        var sizeVariants = group.ModifierItems
            .Where(i => !string.IsNullOrEmpty(i.Size))
            .GroupBy(i => i.Size)
            .Select(g => $"{g.Key} {g.First().Price} dollar")
            .ToList();

        if (sizeVariants.Any())
        {
            stringBuilder.AppendLine($"{group.GroupName} (Size): {string.Join(", ", sizeVariants)}.");
            AppendTimePeriod(group, stringBuilder);
            stringBuilder.AppendLine();
        }
    }

    private void AppendTimePeriod(RestaurantMenuItemSpecificationDto group, StringBuilder stringBuilder)
    {
        if (group.TimePeriods != null && group.TimePeriods.Any())
        {
            var timePeriodDescriptions = group.TimePeriods.Select(tp =>
            {
                var days = string.Join(", ", tp.DayOfWeeks.Select(ConvertDayOfWeekToName));
                return $"{tp.Name} ({tp.StartTime} - {tp.EndTime}, Days: {days})";
            });

            stringBuilder.AppendLine($"Available during: {string.Join("; ", timePeriodDescriptions)}");
            stringBuilder.AppendLine();
        }
    }
    
    private string ConvertDayOfWeekToName(int day)
    {
        return day switch
        {
            0 => "Sunday",
            1 => "Monday",
            2 => "Tuesday",
            3 => "Wednesday",
            4 => "Thursday",
            5 => "Friday",
            6 => "Saturday",
            _ => $"Unknown({day})"
        };
    }
}