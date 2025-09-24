namespace SmartTalk.Messages.Dto.Sales;

public class GetCustomerLevel5HabitResponseDto
{
    public List<HistoryCustomerLevel5HabitDto> HistoryCustomerLevel5HabitDtos { get; set; } 
}

public class HistoryCustomerLevel5HabitDto
{ 
    public string CustomerId { get; set; }
    
    public string LevelCode5 { get; set; }
    
    public DateTime CreateDate { get; set; }
    
    public string? CustomerLikeName { get; set; }
}