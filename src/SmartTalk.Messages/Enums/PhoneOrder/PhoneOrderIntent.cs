using System.ComponentModel;

namespace SmartTalk.Messages.Enums.PhoneOrder;

public enum PhoneOrderIntent
{
    [Description("闲聊")]
    Chat = 0,
    
    [Description("加单")]
    AddOrder = 1,
    
    [Description("减单")]
    ReduceOrder = 2,
    
    [Description("default")]
    Default = 9999
}