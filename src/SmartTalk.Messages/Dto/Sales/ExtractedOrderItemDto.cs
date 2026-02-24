namespace SmartTalk.Messages.Dto.Sales;

public class ExtractedOrderDto
{
    public string StoreName { get; set; } = string.Empty;

    public string StoreNumber { get; set; } = string.Empty;

    public DateTime DeliveryDate { get; set; }
    
    public bool IsDeleteWholeOrder { get; set; }
    
    public bool IsUndoCancel { get; set; } 

    public List<ExtractedOrderItemDto> Orders { get; set; } = new();
}

public class ExtractedOrderItemDto
{
    public string Name { get; set; }

    public int Quantity { get; set; } 

    public string MaterialNumber { get; set; }

    public string Unit { get; set; }
    
    public bool MarkForDelete { get; set; } 
    
    public bool Restored { get; set; }
}