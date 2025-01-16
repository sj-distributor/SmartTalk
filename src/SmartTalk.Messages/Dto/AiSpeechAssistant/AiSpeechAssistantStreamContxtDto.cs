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
    
    public AiSpeechAssistantStreamContxtUserInfoDto UserInfo { get; set; }

    public List<AiSpeechAssistantStreamContxtOrderItemDto> OrderItems { get; set; } = [];
}

public class AiSpeechAssistantStreamContxtUserInfoDto
{
    public string UserName { get; set; }
    
    public string PhoneNumber { get; set; }
    
    public string Address { get; set; }
    
    public string DeliveryTime { get; set; }
    
    public AiSpeechAssistantDeliverType DeliveryType { get; set; }
}

public class AiSpeechAssistantStreamContxtOrderItemDto
{
    public string Name { get; set; }
    
    public decimal Price { get; set; }
    
    public int Quantity { get; set; }
    
    public string Comment { get; set; }
}
