using Mediator.Net.Contracts;

namespace SmartTalk.Messages.Commands.Twilio;

public class HandlePhoneCallStatusCallBackCommand : ICommand
{
   public string CallSid { get; set; }
    
   public string From { get; set; }
    
   public string To { get; set; }
    
   public string Status { get; set; }
    
   public string Direction { get; set; }
}