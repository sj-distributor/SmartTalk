using SmartTalk.Messages.Enums.PhoneOrder;

namespace SmartTalk.Messages.Dto.SpeechMatics;

public class SpeechMaticsSpeakInfoDto
{
    public double StartTime { get; set; }
    
    public double EndTime { get; set; }
    
    public string Speaker { get; set; }
    
    public PhoneOrderRole Role { get; set; }

    public string Text { get; set; }
}

public class SpeechMaticsSpeakInfoForAutoTestDto
{
    public double StartTime { get; set; }
    
    public double EndTime { get; set; }
    
    public string Speaker { get; set; }

    public byte[] Audio { get; set; }
}
