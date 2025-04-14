using Mediator.Net.Contracts;

namespace SmartTalk.Messages.Requests.Restaurant;

public class GetModifierProductsPromptRequest : IRequest
{
    public string RestaurantName{ get; set; }
    
    public string LanguageCode { get; set; }
    
    public DateTime CurrentTime { get; set; }
}

public class GetModifierProductsPromptResponse : IResponse
{
    public List<LocalizedPrompt> Prompts { get; set; }
}

public class LocalizedPrompt
{
    public string LanguageCode { get; set; }
    
    public string Prompt { get; set; }
}