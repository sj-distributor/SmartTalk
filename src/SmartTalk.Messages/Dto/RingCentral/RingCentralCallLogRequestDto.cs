using SmartTalk.Messages.Enums.RingCentral;

namespace SmartTalk.Messages.Dto.RingCentral;

public class RingCentralCallLogRequestDto
{
    public string PhoneNumber { get; set; }
    
    public RingCentralCallDirection? Direction { get; set; }
    
    public RingCentralCallType? Type { get; set; }
    
    public RingCentralCallView? View { get; set; }
    
    public bool? WithRecording { get; set; }
    
    public DateTime? DateFrom { get; set; }
    
    public DateTime? DateTo { get; set; }
    
    public int? Page { get; set; } = 1;
    
    public int? PerPage { get; set; } = 100;
}