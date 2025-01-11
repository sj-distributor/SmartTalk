using SmartTalk.Messages.Dto.Account;
using SmartTalk.Messages.Enums.PhoneOrder;

namespace SmartTalk.Messages.Dto.PhoneCall;

public class PhoneCallRecordDto
{
    public int Id { get; set; }

    public string SessionId { get; set; }
    
    public PhoneCallRecordStatus Status { get; set; }

    public PhoneCallRestaurant Restaurant { get; set; }
    
    public string Tips { get; set; }

    public string TranscriptionText { get; set; }
    
    public long? ManualOrderId { get; set; }
    
    public string Url { get; set; }
    
    public int? LastModifiedBy { get; set; }

    public DateTimeOffset CreatedDate { get; set; }
    
    public UserAccountDto UserAccount { get; set; }
    
    public string TranscriptionJobId { get; set; }
}