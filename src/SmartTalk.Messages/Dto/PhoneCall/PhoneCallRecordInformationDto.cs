using SmartTalk.Messages.Enums.PhoneCall;

namespace SmartTalk.Messages.Dto.PhoneCall;

public class PhoneCallRecordInformationDto
{
    public PhoneCallRestaurant Restaurant { get; set; }
    
    public DateTimeOffset OrderDate { get; set; }
}