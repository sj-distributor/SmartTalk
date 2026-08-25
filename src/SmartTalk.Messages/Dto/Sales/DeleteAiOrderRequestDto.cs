namespace SmartTalk.Messages.Dto.Sales;

public class DeleteAiOrderRequestDto
{
    public string CustomerNumber { get; set; } = string.Empty;
    
    public string SoldToIds { get; set; } = string.Empty;
    
    public DateTime? DeliveryDate { get; set; }
    
    public int AiAssistantId { get; set; }
}

public class DeleteAiOrderResponseDto
{
    public int Code { get; set; }
    
    public string Message { get; set; }
    
    public Guid Data { get; set; }
}