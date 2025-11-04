namespace SmartTalk.Messages.Dto.AutoTest;

public class AutoTestDataItemInputJsonDto
{
    public string Recording { get; set; }
    
    public string OrderId { get; set; }
    
    public string CustomerId { get; set; }
    
    public List<OrderDetailItem> Detail { get; set; }

    public string Desc { get; set; }
}

public class OrderDetailItem
{
    public int SerialNumber { get; set; }
    
    public int Quantity { get; set; }
    
    public double ItemDesc { get; set; }
}
