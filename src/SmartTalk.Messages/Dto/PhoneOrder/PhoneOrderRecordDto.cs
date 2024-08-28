using SmartTalk.Messages.Dto.Account;
using SmartTalk.Messages.Enums.PhoneOrder;

namespace SmartTalk.Messages.Dto.PhoneOrder;

public class PhoneOrderRecordDto
{
    public int Id { get; set; }

    public string SessionId { get; set; }

    public PhoneOrderRestaurant Restaurant { get; set; }
    
    public string Tips { get; set; }

    public string TranscriptionText { get; set; }
    
    public string Url { get; set; }
    
    public int? LastModifiedBy { get; set; }

    public DateTimeOffset CreatedDate { get; set; }
    
    public UserAccountDto UserAccount { get; set; }
    
    public string TranscriptionJobId { get; set; }
}