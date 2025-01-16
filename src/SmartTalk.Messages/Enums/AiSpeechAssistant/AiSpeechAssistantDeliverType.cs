using System.ComponentModel;

namespace SmartTalk.Messages.Enums.AiSpeechAssistant;

public enum AiSpeechAssistantDeliverType
{
    [Description("堂食")]
    DineIn,
    
    [Description("外卖")]
    Pickup
}