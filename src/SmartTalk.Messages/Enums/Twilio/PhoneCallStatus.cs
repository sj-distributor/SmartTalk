using System.ComponentModel;

namespace SmartTalk.Messages.Enums.Twilio;

public enum PhoneCallStatus
{
    [Description("queued")]
    Queued = 10,
    
    [Description("initiated")]
    Initiated = 20,
    
    [Description("ringing")]
    Ringing = 30,
    
    [Description("in-progress")]
    InProgress = 40,
    
    [Description("busy")]
    Busy = 50,
    
    [Description("failed")]
    Failed = 60,
    
    [Description("no-answer")]
    NoAnswer = 70,
    
    [Description("Cancelled")]
    Cancelled = 80,
    
    [Description("answered")]
    Answered = 90
}