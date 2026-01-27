using System.ComponentModel;

namespace SmartTalk.Messages.Enums.PhoneOrder;

public enum TaskType
{
    [Description("订单审核")]
    Order = 0,
    
    [Description("通知审核")]
    InformationNotification = 1,
    
    [Description("todo")]
    Todo = 2
}