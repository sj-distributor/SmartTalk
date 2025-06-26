using Mediator.Net.Contracts;

namespace SmartTalk.Messages.Requests.Restaurant;

public class GetRestaurantMenuItemSpecificationRequest : IRequest
{
    public string RestaurantName{ get; set; }
    
    public string LanguageCode { get; set; }
}

public class GetRestaurantMenuItemSpecificationResponse : IResponse
{
    public List<LocalizedPrompt> Prompts { get; set; }
}

public class LocalizedPrompt
{
    public string LanguageCode { get; set; }
    
    public string Prompt { get; set; }
}