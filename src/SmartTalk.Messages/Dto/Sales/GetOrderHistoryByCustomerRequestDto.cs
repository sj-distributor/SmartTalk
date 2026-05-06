namespace SmartTalk.Messages.Dto.Sales;

public class GetOrderHistoryByCustomerRequestDto
{
    public string CustomerNumber { get; set; }
}

public class GetOrderHistoryByCustomerResponseDto : SalesResponseDto
{
    public List<SalesOrderHistoryDto> Data { get; set; }
}

public class SalesOrderHistoryDto
{
    public string InvoiceNumber { get; set; }
    
    public string MaterialNumber { get; set; }
    
    public string IsAssign { get; set; }

    public string MaterialDescription{ get ; set; }
     
    public string SalesUnit { get; set; }
     
    public decimal Price { get; set; }

    public DateTime? LastInvoiceDate { get; set; }

    public string LevelCode { get; set; }
    
    public string Level5 { get; set; }
}