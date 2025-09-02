using Mediator.Net.Contracts;
using SmartTalk.Messages.Enums.Printer;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Message.Commands.Printer;

public class AddMerchPrinterCommand : ICommand
{
    public string PrinterName { get; set; }
    
    public string PrinterMac { get; set; } 
    
    public int StoreId { get; set; }

    public PrinterLanguageType? PrinterLanguage { get; set; } = PrinterLanguageType.EnglishAndChinese;
}

public class AddMerchPrinterResponse : SmartTalkResponse
{
}