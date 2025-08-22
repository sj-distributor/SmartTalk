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
    public string MaterialNumber { get; set; }
    
    public string MaterialDescription{ get ; set; }
     
    public string SalesUnit { get; set; }
     
    public string LatestSoNumber { get; set; }
     
    public decimal LatestSoCost { get; set; }
     
    public DateTime LatestSoDocDate { get; set; }
     
    public decimal PriceLatestOrderPrice { get; set; }
     
    public decimal Qty2WkAgo { get; set; }
     
    public decimal Qty3WkAgo { get; set; }
     
    public decimal Qty4WkAgo { get; set; }
     
    public decimal QtyLastWeek { get; set; }
}