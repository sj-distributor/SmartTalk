using Mediator.Net.Contracts;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Message.Commands.Printer;
public class AddMerchPrinterCommand : ICommand
{
    public string PrinterName { get; set; }
    
    public string PrinterMac { get; set; } 
    
    public int StoreId { get; set; }
}

public class AddMerchPrinterResponse : SmartTalkResponse
{
}