using System.ComponentModel;

namespace SmartTalk.Messages.Enums.PhoneOrder;

public enum PhoneOrderIntent
{
    [Description("闲聊")]
    Chat = 0,

    [Description("問菜品")]
    AskDishes= 1,
    
    [Description("打招呼")]
    SayHi= 2,

    [Description("转人工")]
    TransferToHuman= 3,
    
    [Description("下單")]
    Order= 4,
    
    [Description("营业时间")]
    AskOpeningHours = 5,
    
    [Description("地址")]
    AskAddress = 6,
    
    [Description("gluten free")]
    AskGlutenFree = 7,
    
    [Description("加单")]
    AddOrder = 8,
    
    [Description("减单")]
    ReduceOrder = 9,
    
    [Description("问单")]
    AskShoppingCart = 10,
    
    [Description("欢送语")]
    SayBye = 11,
    
    [Description("问味精")]
    AskMSG = 12,

    [Description("default")]
    Default= 9999
}