using Mediator.Net.Contracts;
using SmartTalk.Messages.Attributes;
using SmartTalk.Messages.Constants;
using SmartTalk.Messages.Enums.Printer;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.Printer;

[SmartTalkAuthorize(Permissions = new []{ SecurityStore.Permissions.CanViewMerchPrinter})]
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