using Newtonsoft.Json;
using SmartTalk.Messages.Enums.AiSpeechAssistant;

namespace SmartTalk.Messages.Dto.AiSpeechAssistant;

public class AiSpeechAssistantStreamContextDto
{
    public string StreamSid { get; set; }

    public string Host { get; set; }
    
    public int LatestMediaTimestamp { get; set; } = 0;
        
    public string LastAssistantItem { get; set; }

    public Queue<string> MarkQueue { get; set; } = new Queue<string>();

    public long? ResponseStartTimestampTwilio { get; set; } = null;
        
    public bool InitialConversationSent { get; set; } = false;

    public bool ShowTimingMath { get; set; } = true;
    
    public AiSpeechAssistantUserInfoDto UserInfo { get; set; }
    
    public AiSpeechAssistantOrderDto OrderItems { get; set; }
    
    public AiSpeechAssistantUserInfoDto LastUserInfo { get; set; }

    public string OrderItemsJson { get; set; } = "No orders yet";

    public string LastPrompt { get; set; }

    public object LastMessage { get; set; }
    
    public string CallSid { get; set; }
    
    public string HumanContactPhone { get; set; }
    
    public string ForwardPhoneNumber { get; set; }
    
    public bool ShouldForward { get; set; } = false;
    
    public AiSpeechAssistantDto Assistant { get; set; }
    
    public AiSpeechAssistantKnowledgeDto Knowledge { get; set; }

    public List<(AiSpeechAssistantSpeaker, string)> ConversationTranscription { get; set; } = new();
    
    public bool IsTransfer { get; set; } = false;
    
    public string CustomerItemsString { get; set; }
    
    public bool IsInAiServiceHours { get; set; } = true;
    
    public string TransferCallNumber { get; set; }
}

public class AiSpeechAssistantUserInfoDto
{
    [JsonProperty("customer_name")]
    public string UserName { get; set; } = "Unknown yet";
    
    [JsonProperty("customer_phone")]
    public string PhoneNumber { get; set; }
    
    [JsonProperty("customer_address")]
    public string Address { get; set; }
}

public class AiSpeechAssistantOrderDto
{
    [JsonProperty("comments")]
    public string Comments { get; set; }
    
    [JsonProperty("order_items")]
    public List<AiSpeechAssistantOrderItemDto> Order { get; set; }
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
}