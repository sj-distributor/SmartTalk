namespace SmartTalk.Messages.Dto.Sales;

public class GenerateAiOrdersRequestDto
{
    public string AiModel { get; set; }
    
    public AiOrderInfoDto AiOrderInfoDto { get; set; }
    
    public bool UseCanceledOrder { get; set; }
}

public class AiOrderInfoDto
{
    public string SoldToId { get; set; } 
    
    public DateTime DocumentDate { get; set; }
    
    public DateTime DeliveryDate { get; set; }
    
    public string SoldToIds { get; set; }
     
    public List<AiOrderItemDto> AiOrderItemDtoList { get; set; } = new();
}

public class AiOrderItemDto
{
    public string MaterialNumber { get; set; }
    
    public string AiMaterialDesc { get; set; }
    
    public string AiSource { get; set; }
    
    public decimal? Price { get; set; } = null;
     
    public int MaterialQuantity { get; set; } = 1;

    public string AiUnit { get; set; }
    
    public bool MarkForDelete { get; set; }
}