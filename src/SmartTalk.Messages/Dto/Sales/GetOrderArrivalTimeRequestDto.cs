namespace SmartTalk.Messages.Dto.Sales;

public class GetOrderArrivalTimeRequestDto
{
    public List<string> CustomerIds { get; set; }
}

public class GetOrderArrivalTimeResponseDto :SalesResponseDto
{
    public List<GetOrderArrivalTimeDataDto> Data { get; set; }
}

public class GetOrderArrivalTimeDataDto
{
    public string CustomerId { get; set; }

    public string SalesOrderNumber { get; set; }
    
    public int OrderStatus { get; set; }
    
    public DateTime ActualDeliveryTime { get; set; }
    
    public DateTime EstimatedDeliveryTime { get; set; }
}