namespace SmartTalk.Messages.Dto.PhoneOrder;

public class ReservationAiResultDto
{
    public string ReservationDate { get; set; }

    public string ReservationTime { get; set; }
    
    public string UserName { get; set; }
    
    public int? PartySize { get; set; }
    
    public string SpecialRequests { get; set; }
}