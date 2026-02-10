namespace SmartTalk.Messages.Dto.Sales;

public class GetAiOrderItemsByDeliveryDateRequestDto
{
    public string CustomerNumber { get; set; }
    
    public DateTime DeliveryDate { get; set; }
}

public class GetAiOrderItemsByDeliveryDateResponseDto
{
    public int Code { get; set; }

    public string Message { get; set; }

    public List<AiOrderItemSimpleDto> Data { get; set; }
}

public class AiOrderItemSimpleDto
{
    public string MaterialNumber { get; set; }

    public string MaterialDescription { get; set; }
    
    public string AiMaterialDesc { get; set; }

    public int MaterialQuantity { get; set; }
    
    public string AiUnit { get; set; }

    public bool MarkForDelete { get; set; }

    public bool Restored { get; set; }
}
