using Newtonsoft.Json;
using SmartTalk.Messages.Enums.AiSpeechAssistant;

namespace SmartTalk.Messages.Dto.AiSpeechAssistant;

public class AiSpeechAssistantStreamContxtDto
{
    public string StreamSid { get; set; }

    public int LatestMediaTimestamp { get; set; } = 0;
        
    public string LastAssistantItem { get; set; }

    public Queue<string> MarkQueue { get; set; } = new Queue<string>();

    public long? ResponseStartTimestampTwilio { get; set; } = null;
        
    public bool InitialConversationSent { get; set; } = false;

    public bool ShowTimingMath { get; set; } = false;
    
    public AiSpeechAssistantUserInfoDto UserInfo { get; set; }
    
    public AiSpeechAssistantOrderDto OrderItems { get; set; }

    public AiSpeechAssistantUserInfoDto LastUserInfo { get; set; }

    public string OrderItemsJson { get; set; } = "No orders yet";

    public string LastPrompt { get; set; }
}

public class AiSpeechAssistantUserInfoDto
{
    public string UserName { get; set; }
    
    public string PhoneNumber { get; set; }
}

public class AiSpeechAssistantOrderDto
{
    [JsonProperty("after_modified_order_items")]
    public List<AiSpeechAssistantOrderItemDto> Order { get; set; }
}

public class AiSpeechAssistantOrderItemDto
{
    public string Name { get; set; }
    
    public decimal Price { get; set; }
    
    public int Quantity { get; set; }
    
    public string Comments { get; set; }
    
    public string Specification { get; set; }
    
    public AiSpeechAssistantOrderType OrderType { get; set; }
}
