using System.ComponentModel;

namespace SmartTalk.Messages.Enums.AiSpeechAssistant;

public enum AiSpeechAssistantChannel
{
   [Description("文字")]
   Text,
   
   [Description("電話")]
   PhoneCall,
   
   [Description("實時聊天")]
   LiveChat
}