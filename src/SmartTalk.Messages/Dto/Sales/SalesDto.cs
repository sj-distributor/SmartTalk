using SmartTalk.Messages.Enums.Sales;

namespace SmartTalk.Messages.Dto.Sales;

public class SalesDto
{
    public int Id { get; set; }
    
    public string Name { get; set; }
    
    public SalesCallType Type { get; set; }
    
    public int? CreatedBy { get; set; }
    
    public DateTimeOffset CreatedDate { get; set; }
}