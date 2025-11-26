namespace SmartTalk.Messages.Dto.Sales;

public class ExtractedOrderDto
{
    public string StoreName { get; set; } = string.Empty;

    public string StoreNumber { get; set; } = string.Empty;

    public DateTime DeliveryDate { get; set; }

    public List<ExtractedOrderItemDto> Orders { get; set; } = new();
}

public class ExtractedOrderItemDto
{
    public string Name { get; set; }

    public int Quantity { get; set; } 

    public string MaterialNumber { get; set; }

    public string Unit { get; set; }
}