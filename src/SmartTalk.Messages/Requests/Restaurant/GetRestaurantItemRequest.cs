using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.Restaurant;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.Restaurant;

public class GetRestaurantMenuItemsRequest : IRequest
{
    public int? PageIndex { get; set; }
    
    public int? PageSize { get; set; }
    
    public string Keyword { get; set; }
    
    public int RestaurantId { get; set; }
}

public class GetRestaurantMenuItemsResponse : SmartTalkResponse<List<RestaurantMenuItemDto>>
{
}