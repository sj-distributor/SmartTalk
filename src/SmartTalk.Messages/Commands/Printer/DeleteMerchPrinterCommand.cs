using Mediator.Net.Contracts;
using SmartTalk.Messages.Attributes;
using SmartTalk.Messages.Constants;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.Printer;

[SmartTalkAuthorize(Permissions = new []{ SecurityStore.Permissions.CanViewMerchPrinter})]
public class DeleteMerchPrinterCommand : ICommand
{
    public int Id { get; set; }
}

public class DeleteMerchPrinterResponse : SmartTalkResponse
{
}