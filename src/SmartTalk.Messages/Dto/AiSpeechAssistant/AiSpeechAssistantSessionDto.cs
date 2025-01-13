namespace SmartTalk.Messages.Dto.AiSpeechAssistant;

public class AiSpeechAssistantSessionDto
{
    public string Type { get; set; }
    
    public Session Session { get; set; }
}

public class Session
{
    public TurnDetection TurnDetection { get; set; }
    
    public string InputAudioFormat { get; set; }
    
    public string OutputAudioFormat { get; set; }
    
    public string Voice { get; set; }
    
    public string Instructions { get; set; }
    
    public List<string> Modalities { get; set; }
    
    public double Temperature { get; set; }
    
    public string ToolChoice { get; set; }
    
    public List<Tool> Tools { get; set; }
}

public class TurnDetection
{
    public string Type { get; set; }
}

public class Tool
{
    public string Type { get; set; }
    
    public string Name { get; set; }
    
    public string Description { get; set; }
    
    public ToolParameters Parameters { get; set; }
}

public class ToolParameters
{
    public string Type { get; set; }
    
    public ToolProperties Properties { get; set; }
}

public class ToolProperties
{
    public OrderedItems OrderedItems { get; set; }
}

public class OrderedItems
{
    public string Type { get; set; }
    
    public string Description { get; set; }
    
    public OrderedItemProperties Items { get; set; }
}

public class OrderedItemProperties
{
    public ItemProperty ItemName { get; set; }
    
    public ItemProperty Count { get; set; }
    
    public ItemProperty Comment { get; set; }
    
    public ItemProperty SerialNumber { get; set; }
}

public class ItemProperty
{
    public string Type { get; set; }
    
    public string Description { get; set; }
}