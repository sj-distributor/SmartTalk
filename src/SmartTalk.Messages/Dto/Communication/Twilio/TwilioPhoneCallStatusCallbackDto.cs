using SmartTalk.Messages.Enums.Twilio;

namespace SmartTalk.Messages.DTO.Communication.Twilio;

public class TwilioPhoneCallStatusCallbackDto : ICommunicationPhoneCallStatusCallbackDto
{
    public string CallSid { get; set; }
    
    public string From { get; set; }
    
    public string To { get; set; }
    
    public string Status { get; set; }
    
    public string Direction { get; set; }
}