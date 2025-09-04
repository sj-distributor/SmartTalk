using Mediator.Net.Contracts;
using SmartTalk.Messages.Attributes;
using SmartTalk.Messages.Constants;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.Printer;

[SmartTalkAuthorize(Permissions = new []{ SecurityStore.Permissions.CanViewMerchPrinter})]
public class PrintTestCommand : ICommand
{
    public int StoreId { get; set; }

    public string PrinterMac { get; set; }
}

public class PrintTestResponse : SmartTalkResponse
{
}