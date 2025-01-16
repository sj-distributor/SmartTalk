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

    public List<AiSpeechAssistantOrderItemDto> OrderItems { get; set; } = [];

    public AiSpeechAssistantUserInfoDto LastUserInfo { get; set; }

    public List<AiSpeechAssistantOrderItemDto> LasterOrderItems { get; set; }

    public string OriginalPrompt { get; set; }
}

public class AiSpeechAssistantUserInfoDto
{
    [JsonProperty("customer_name")]
    public string UserName { get; set; }
    
    [JsonProperty("customer_phone")]
    public string PhoneNumber { get; set; }
    
    public string Address { get; set; }
}

public class AiSpeechAssistantOrderItemDto
{
    [JsonProperty("item_name")]
    public string Name { get; set; }
    
    [JsonProperty("price")]
    public decimal Price { get; set; }
    
    [JsonProperty("quantity")]
    public int Quantity { get; set; }
    
    [JsonProperty("notes")]
    public string Comments { get; set; }
    
    [JsonProperty("specification")]
    public string Specification { get; set; }
    
    public AiSpeechAssistantOrderType OrderType { get; set; }
}
