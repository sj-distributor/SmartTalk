using SmartTalk.Messages.Enums.PhoneOrder;

namespace SmartTalk.Messages.Dto.PhoneOrder;

public class PhoneOrderRecordInformationDto
{
    public PhoneOrderRestaurant Restaurant { get; set; }
    
    public DateTimeOffset OrderDate { get; set; }
}