namespace SmartTalk.Messages.Dto.Sales;

public class GetAskInfoDetailListByCustomerRequestDto
{
    public List<string> CustomerNumbers { get; set; }
}

public class GetAskInfoDetailListByCustomerResponseDto : SalesResponseDto
{
    public List<VwAskDetail> Data { get; set; }
}
     
public class VwAskDetail
{
    public string Plant { get; set; }
    
    public string Material { get; set; }
    
    public string CustomerId { get; set; }
    
    public string Name1 { get; set; }
    
    public string MaterialDesc { get; set; }
    
    public int Status { get; set; }
    
    public string MaterialType { get; set; }
    
    public string CustomerType { get; set; }
    
    public int ValidAskQty { get; set; }
    
    public int AskQty { get; set; }
    
    public int AiTrainedQuantity { get; set; }
    
    public int AskReference { get; set; }
    
    public int WeekQty { get; set; }
    
    public int WeekDays { get; set; }
    
    public int OverReference { get; set; }
     
    public int TodayQty { get; set; }
    
    public int AskDays { get; set; }
     
    public string Comments { get; set; }
    
    public DateTime LastUpdate { get; set; }
     
    public DateTime ReadTime { get; set; }
     
    public string SalesGroup { get; set; }
     
    public string LevelCode { get; set; }
     
    public string LevelText { get; set; }
    
    public double Atr { get; set; }
}