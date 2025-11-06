namespace SmartTalk.Messages.Dto.AutoTest;

public class SalesOrderDto
{
    public string Recording { get; set; }
    
    public string OrderId { get; set; }
    
    public string CustomerId { get; set; }
    
    public List<SalesOrderDetailItem> Detail { get; set; }

    public string Desc { get; set; }
}

public class SalesOrderDetailItem
{
    public int SerialNumber { get; set; }
    
    public int Quantity { get; set; }
    
    public double ItemDesc { get; set; }
}
