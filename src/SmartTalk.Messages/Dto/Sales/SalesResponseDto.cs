namespace SmartTalk.Messages.Dto.Sales;

public class SalesResponseDto
{ 
    public int Code { get; set; }
    
    public string Message { get; set; }
    
    public GenerateAiOrderResult Data { get; set; }
}

public class GenerateAiOrderResult
{
    public Guid OrderId { get; set; }
    
    public bool IsSuccessAddItem { get; set; }
}