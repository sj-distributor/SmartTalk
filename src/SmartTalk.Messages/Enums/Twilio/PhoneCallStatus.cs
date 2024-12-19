using System.ComponentModel;

namespace SmartTalk.Messages.Enums.Twilio;

public enum PhoneCallStatus
{
    Queued = 10,
    
    Initiated = 20,
    
    Ringing = 30,
    
    [Description("in-progress")]
    InProgress = 40,
    
    Busy = 50,
    
    Failed = 60,
    
    [Description("no-answer")]
    NoAnswer = 70,
    
    Cancelled = 80,
    
    Completed = 90
}