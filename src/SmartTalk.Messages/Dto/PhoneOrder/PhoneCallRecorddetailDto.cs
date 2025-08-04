namespace SmartTalk.Messages.Dto.PhoneOrder;

public class PhoneCallRecordDetailDto
{
    public string Name { get; set; }
    
    public List<PhoneOrderRecordDto> Records { get; set; }
}