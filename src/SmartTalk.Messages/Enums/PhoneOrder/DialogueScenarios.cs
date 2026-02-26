using System.ComponentModel;

namespace SmartTalk.Messages.Enums.PhoneOrder;

public enum DialogueScenarios
{
    [Description("订位")]
    Reservation = 0,
    
    [Description("订餐")]
    Order = 1,
    
    [Description("咨询")]
    Inquiry = 2,
    
    [Description("第三方订单通知")]
    ThirdPartyOrderNotification = 3,
    
    [Description("投诉反馈")]
    ComplaintFeedback = 4,
    
    [Description("信息通知")]
    InformationNotification = 5,
    
    [Description("转接人工客服")]
    TransferToHuman = 6,
    
    [Description("推销电话")]
    SalesCall = 7,
    
    [Description("无效来电")]
    InvalidCall = 8,
    
    [Description("转接语音信箱")]
    TransferVoicemail = 9,
    
    [Description("其他")]
    Other  = 10,  
    
    [Description("待办事项")]
    ToDoTask
}