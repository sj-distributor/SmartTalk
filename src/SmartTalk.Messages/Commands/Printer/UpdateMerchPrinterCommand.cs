using Mediator.Net.Contracts;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.Printer;

public class UpdateMerchPrinterCommand : ICommand
{
    public int Id { get; set; }

    public int StoreId { get; set; }

    public string PrinterName { get; set; }
    
    public bool IsEnabled { get; set; }
}

public class UpdateMerchPrinterResponse : SmartTalkResponse
{
}