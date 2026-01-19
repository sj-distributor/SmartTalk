using SmartTalk.Messages.Enums.Printer;

namespace SmartTalk.Messages.Dto.Printer;

public class MerchPrinterOrderDto
{
    public Guid Id { get; set; }
        
    public int StoreId { get; set; }

    public int? OrderId { get; set; }
    
    public int? PhoneOrderId { get; set; }

    public PrintStatus PrintStatus { get; set; }

    public int PrintErrorTimes { get; set; }
       
    public DateTimeOffset PrintDate { get; set; }
        
    public PrintFormat PrintFormat { get; set; }
    
    public DateTimeOffset CreatedDate { get; set; }

    public bool IsPrintTest()
    {
        return OrderId is 0 && PhoneOrderId is 0;
    }
}