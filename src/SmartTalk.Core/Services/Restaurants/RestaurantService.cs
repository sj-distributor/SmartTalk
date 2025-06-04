using System.Text;
using AutoMapper;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Domain.Restaurants;
using SmartTalk.Messages.Commands.Restaurants;
using SmartTalk.Core.Services.RetrievalDb.VectorDb;
using SmartTalk.Messages.Dto.EasyPos;
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
        
        foreach (var group in groups.DistinctBy(g => g.GroupName))
        {
            if (!promptDict.ContainsKey(group.LanguageCode))
                promptDict[group.LanguageCode] = new StringBuilder();

            var stringBuilder = promptDict[group.LanguageCode];
            
            AppendGroupDescription(group, stringBuilder);
            AppendItemDescription(group, stringBuilder);
            AppendSizeVariants(group, stringBuilder);
            AppendTimePeriodDescription(group.TimePeriods, stringBuilder);
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
            
            stringBuilder.Append($"{group.GroupName}, priced at {group.ItemPrice} dollar, served with your choice of {group.MinimumSelect} sides: {sides}. Choose exactly {group.MinimumSelect}. ");
            
            if (group.MaximumRepetition == 1)
            {
                stringBuilder.Append("Each item can only be selected once. ");
            }
            else if (group.MaximumRepetition == 0)
            {
                stringBuilder.Append("Items can be selected multiple times. ");
            }
        }
    }

    private void AppendItemDescription(RestaurantMenuItemSpecificationDto group, StringBuilder stringBuilder)
    {
        foreach (var item in group.ModifierItems)
        {
            var desc = item.Price > 0 
                ? $"{item.Name} (add-on {item.Price} dollar)" 
                : $"{item.Name} (included)";
            stringBuilder.AppendLine(desc);
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
        }
    }
    
    private void AppendTimePeriodDescription(List<EasyPosResponseTimePeriod> timePeriods, StringBuilder stringBuilder)
    {
        if (timePeriods == null || !timePeriods.Any()) return;

        foreach (var period in timePeriods)
        {
            var days = string.Join(", ", period.DayOfWeeks.Select(ConvertDayOfWeekToName));
            stringBuilder.AppendLine($"{period.Name} ({period.StartTime} - {period.EndTime}, Days: {days}) ");
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