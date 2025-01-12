using SmartTalk.Messages.Enums.PhoneCall;

namespace SmartTalk.Messages.Dto.SpeechMatics;

public class SpeechMaticsSpeakInfoDto
{
    public double StartTime { get; set; }
    
    public double EndTime { get; set; }
    
    public string Speaker { get; set; }
    
    public PhoneCallRole Role { get; set; }
}