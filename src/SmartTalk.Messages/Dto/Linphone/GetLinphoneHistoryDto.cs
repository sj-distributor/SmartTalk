using SmartTalk.Messages.Enums.Linphone;

namespace SmartTalk.Messages.Dto.Linphone;

public class GetLinphoneHistoryDto
{
    public int Id { get; set; }
    
    public long CallDate { get; set; }

    public string Caller { get; set; }
    
    public string Targetter { get; set; }

    public LinphoneStatus Status { get; set; }
}