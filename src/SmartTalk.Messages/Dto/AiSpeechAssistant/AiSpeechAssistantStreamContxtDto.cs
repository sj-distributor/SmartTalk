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

    public AiSpeechAssistantComplaintInfoDto ComplaintInfo { get; set; } = new();
    
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

    public List<string> CandidateCustomerIds { get; set; } = [];

    public List<string> MatchedCustomerIds { get; set; } = [];

    public List<(AiSpeechAssistantSpeaker, string)> ConversationTranscription { get; set; } = new();
    
    public bool IsTransfer { get; set; } = false;
    
    public string CustomerItemsString { get; set; }
    
    public bool IsInAiServiceHours { get; set; } = true;
    
    public string TransferCallNumber { get; set; }
    
    public bool IsEnableManualService { get; set; }
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

public class AiSpeechAssistantComplaintInfoDto
{
    [JsonProperty("invoice_numbers")]
    public List<string> InvoiceNumbers { get; set; } = [];

    [JsonProperty("products")]
    public List<string> Products { get; set; } = [];

    [JsonProperty("problem_description")]
    public string ProblemDescription { get; set; }

    [JsonProperty("affected_quantity")]
    public string AffectedQuantity { get; set; }

    [JsonProperty("delivery_date")]
    public string DeliveryDate { get; set; }

    [JsonProperty("delivery_date_text")]
    public string DeliveryDateText { get; set; }

    [JsonProperty("is_confirmed")]
    public bool? IsConfirmed { get; set; }
}
