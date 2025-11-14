using SmartTalk.Messages.Dto.Account;
using SmartTalk.Messages.Enums.PhoneOrder;
using SmartTalk.Messages.Enums.STT;

namespace SmartTalk.Messages.Dto.PhoneOrder;

public class PhoneOrderRecordDto
{
    public int Id { get; set; }

    public string SessionId { get; set; }
    
    public PhoneOrderRecordStatus Status { get; set; }
    
    public string Tips { get; set; }

    public string TranscriptionText { get; set; }
    
    public long? ManualOrderId { get; set; }
    
    public string Url { get; set; }
    
    public int? LastModifiedBy { get; set; }

    public DateTimeOffset CreatedDate { get; set; }
    
    public string TranscriptionJobId { get; set; }
    
    public string LastModifiedByName { get; set; }
    
    public PhoneOrderOrderStatus OrderStatus { get; set; }
    
    public string PhoneNumber { get; set; }
    
    public string CustomerName { get; set; }
    
    public string Comments { get; set; }
    
    public TranscriptionLanguage Language { get; set; }
    
    public double? Duration { get; set; }
    
    public bool? IsTransfer { get; set; }
    
    public string IncomingCallNumber { get; set; }

    public string ConversationText { get; set; }
}