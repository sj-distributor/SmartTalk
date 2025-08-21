namespace SmartTalk.Messages.Dto.Sales;

public class GetCustomerNumbersByNameResponseDto
{
    public int Code { get; set; }
    
    public string Message { get; set; }
    
    public List<CustomerNumberAndName> Data { get; set; }
}

public class CustomerNumberAndName
{
    public string CustomerNumber { get; set; }
    
    public string CustomerName { get; set; }
}