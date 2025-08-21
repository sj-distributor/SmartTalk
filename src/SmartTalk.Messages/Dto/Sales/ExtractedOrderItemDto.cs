namespace SmartTalk.Messages.Dto.Sales;

public class StoreOrderDto
{
    public string StoreName { get; set; }
    
    public string StoreNumber { get; set; }
    
    public List<ExtractedOrderItemDto> Orders { get; set; }
    
    public DateTime DeliveryDate { get; set; }
}

public class ExtractedOrderItemDto
{
    public string Name { get; set; }
    
    public int Quantity { get; set; } 
    
    public string MaterialNumber { get; set; }
}